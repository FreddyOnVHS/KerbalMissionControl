using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using KMC.Shared;
using UnityEngine;

namespace KMC.Plugin
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class ManeuverUplinkReceiver : MonoBehaviour
    {
        private const float StateSendIntervalSeconds = 0.25f;
        private const double NodeUtToleranceSeconds = 0.25;
        private const double DeltaVToleranceMetersPerSecond = 0.05;
        private const int AssessmentSamplesPerPatch = 160;
        private const int AssessmentRefineIterations = 24;
        private const int AssessmentMaxPatches = 12;

        private sealed class TrackedManeuver
        {
            public string VesselId;
            public string PlanId;
            public string TargetBodyName;
            public ManeuverNode Node;
            public double PlannedNodeUt;
            public double PlannedPrograde;
            public double PlannedNormal;
            public double PlannedRadial;

            public bool TransferAssessmentAvailable;
            public bool TargetEncounter;
            public double ClosestApproachMeters = double.NaN;
            public double ClosestApproachUt = double.NaN;
            public double TargetSoiRadiusMeters = double.NaN;
            public double AssessedNodeUt = double.NaN;
            public double AssessedPrograde = double.NaN;
            public double AssessedNormal = double.NaN;
            public double AssessedRadial = double.NaN;
        }

        private readonly object _syncRoot = new object();
        private readonly Dictionary<string, TrackedManeuver> _trackedPlans =
            new Dictionary<string, TrackedManeuver>(StringComparer.Ordinal);

        private UdpClient _receiveClient;
        private UdpClient _ackClient;
        private UdpClient _stateClient;
        private IPEndPoint _ackEndpoint;
        private IPEndPoint _stateEndpoint;
        private Thread _receiveThread;
        private volatile bool _running;
        private ManeuverUplinkPacket _pending;
        private float _nextStateSendTime;

        public void Start()
        {
            try
            {
                _receiveClient = new UdpClient(
                    new IPEndPoint(IPAddress.Loopback, ManeuverUplinkPacket.CommandPort));
                _ackClient = new UdpClient();
                _stateClient = new UdpClient();
                _ackEndpoint = new IPEndPoint(IPAddress.Loopback, ManeuverUplinkPacket.AckPort);
                _stateEndpoint = new IPEndPoint(IPAddress.Loopback, ManeuverUplinkPacket.NodeStatePort);

                _running = true;
                _receiveThread = new Thread(ReceiveLoop);
                _receiveThread.IsBackground = true;
                _receiveThread.Name = "KMC Maneuver Uplink";
                _receiveThread.Start();

                Debug.Log("[KMC] Maneuver uplink / verification receiver started.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[KMC] Maneuver uplink receiver start failed: " + ex);
            }
        }

        public void Update()
        {
            ManeuverUplinkPacket packet = null;
            lock (_syncRoot)
            {
                if (_pending != null)
                {
                    packet = _pending;
                    _pending = null;
                }
            }

            if (packet != null) ApplyManeuver(packet);

            if (Time.realtimeSinceStartup >= _nextStateSendTime)
            {
                _nextStateSendTime = Time.realtimeSinceStartup + StateSendIntervalSeconds;
                PublishTrackedNodeStates();
            }
        }

        private void ReceiveLoop()
        {
            while (_running)
            {
                try
                {
                    IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _receiveClient.Receive(ref sender);
                    string text = Encoding.UTF8.GetString(data);

                    ManeuverUplinkPacket packet;
                    if (!ManeuverUplinkPacket.TryParse(text, out packet)) continue;

                    lock (_syncRoot) _pending = packet;
                }
                catch (ObjectDisposedException) { return; }
                catch (SocketException)
                {
                    if (!_running) return;
                }
                catch (Exception ex)
                {
                    Debug.LogError("[KMC] Maneuver uplink receive failed: " + ex);
                }
            }
        }

        private void ApplyManeuver(ManeuverUplinkPacket packet)
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel == null)
            {
                SendAck(packet, "REJECTED", "NO ACTIVE VESSEL", double.NaN);
                return;
            }

            string activeVesselId = vessel.id.ToString();
            if (!string.Equals(activeVesselId, packet.VesselId, StringComparison.Ordinal))
            {
                SendAck(packet, "REJECTED", "ACTIVE VESSEL ID DOES NOT MATCH PLAN", double.NaN);
                return;
            }

            TrackedManeuver existing;
            if (_trackedPlans.TryGetValue(packet.PlanId, out existing))
            {
                SendAck(packet, "NODE LOADED", "PLAN ALREADY TRACKED - DUPLICATE SUPPRESSED", existing.PlannedNodeUt);
                return;
            }

            double currentUt = Planetarium.GetUniversalTime();
            if (!IsFinite(packet.NodeUniversalTimeSeconds) ||
                packet.NodeUniversalTimeSeconds <= currentUt + 0.25)
            {
                SendAck(packet, "REJECTED", "NODE UT IS NOT IN THE FUTURE", double.NaN);
                return;
            }

            if (!IsFinite(packet.ProgradeDeltaVMetersPerSecond) ||
                !IsFinite(packet.NormalDeltaVMetersPerSecond) ||
                !IsFinite(packet.RadialDeltaVMetersPerSecond))
            {
                SendAck(packet, "REJECTED", "MANEUVER DELTA-V IS INVALID", double.NaN);
                return;
            }

            if (vessel.patchedConicSolver == null)
            {
                SendAck(packet, "REJECTED", "PATCHED CONIC SOLVER UNAVAILABLE", double.NaN);
                return;
            }

            try
            {
                ManeuverNode node =
                    vessel.patchedConicSolver.AddManeuverNode(packet.NodeUniversalTimeSeconds);

                if (node == null)
                {
                    SendAck(packet, "REJECTED", "KSP DID NOT CREATE A MANEUVER NODE", double.NaN);
                    return;
                }

                node.DeltaV = new Vector3d(
                    packet.RadialDeltaVMetersPerSecond,
                    packet.NormalDeltaVMetersPerSecond,
                    packet.ProgradeDeltaVMetersPerSecond);

                vessel.patchedConicSolver.UpdateFlightPlan();

                TrackedManeuver tracked = new TrackedManeuver
                {
                    VesselId = packet.VesselId,
                    PlanId = packet.PlanId,
                    TargetBodyName = packet.TargetBodyName ?? string.Empty,
                    Node = node,
                    PlannedNodeUt = packet.NodeUniversalTimeSeconds,
                    PlannedPrograde = packet.ProgradeDeltaVMetersPerSecond,
                    PlannedNormal = packet.NormalDeltaVMetersPerSecond,
                    PlannedRadial = packet.RadialDeltaVMetersPerSecond
                };

                _trackedPlans.Add(packet.PlanId, tracked);
                UpdateTransferAssessment(tracked);

                SendAck(packet, "NODE LOADED", "PLUGIN CREATED MANEUVER NODE", packet.NodeUniversalTimeSeconds);
                PublishTrackedNodeState(vessel, tracked);

                ScreenMessages.PostScreenMessage(
                    "KMC maneuver node loaded",
                    4f,
                    ScreenMessageStyle.UPPER_CENTER);

                Debug.Log(
                    "[KMC] MANEUVER NODE LOADED" +
                    " | PlanId=" + packet.PlanId +
                    " | VesselId=" + packet.VesselId +
                    " | Target=" + (packet.TargetBodyName ?? string.Empty) +
                    " | NodeUT=" + packet.NodeUniversalTimeSeconds.ToString("0.0") +
                    " | ProgradeDV=" + packet.ProgradeDeltaVMetersPerSecond.ToString("0.00") +
                    " | NormalDV=" + packet.NormalDeltaVMetersPerSecond.ToString("0.00") +
                    " | RadialDV=" + packet.RadialDeltaVMetersPerSecond.ToString("0.00"));
            }
            catch (Exception ex)
            {
                SendAck(packet, "REJECTED", "KSP NODE CREATION FAILED: " + ex.GetType().Name, double.NaN);
                Debug.LogError("[KMC] Maneuver node creation failed: " + ex);
            }
        }

        private void PublishTrackedNodeStates()
        {
            if (_trackedPlans.Count == 0) return;
            Vessel activeVessel = FlightGlobals.ActiveVessel;

            foreach (KeyValuePair<string, TrackedManeuver> pair in _trackedPlans)
                PublishTrackedNodeState(activeVessel, pair.Value);
        }

        private void PublishTrackedNodeState(Vessel activeVessel, TrackedManeuver tracked)
        {
            if (tracked == null) return;

            if (activeVessel == null ||
                !string.Equals(activeVessel.id.ToString(), tracked.VesselId, StringComparison.Ordinal))
            {
                SendNodeState(
                    tracked, "VESSEL NOT ACTIVE", false,
                    double.NaN, double.NaN, double.NaN, double.NaN,
                    "TRACKED MANEUVER VESSEL IS NOT THE ACTIVE VESSEL");
                return;
            }

            PatchedConicSolver solver = activeVessel.patchedConicSolver;
            if (solver == null ||
                solver.maneuverNodes == null ||
                tracked.Node == null ||
                !solver.maneuverNodes.Contains(tracked.Node))
            {
                SendNodeState(
                    tracked, "NODE REMOVED", false,
                    double.NaN, double.NaN, double.NaN, double.NaN,
                    "TRACKED KSP MANEUVER NODE NO LONGER EXISTS");
                return;
            }

            double actualUt = tracked.Node.UT;
            Vector3d actualDeltaV = tracked.Node.DeltaV;
            double actualRadial = actualDeltaV.x;
            double actualNormal = actualDeltaV.y;
            double actualPrograde = actualDeltaV.z;

            bool utMatches = Math.Abs(actualUt - tracked.PlannedNodeUt) <= NodeUtToleranceSeconds;
            bool progradeMatches = Math.Abs(actualPrograde - tracked.PlannedPrograde) <= DeltaVToleranceMetersPerSecond;
            bool normalMatches = Math.Abs(actualNormal - tracked.PlannedNormal) <= DeltaVToleranceMetersPerSecond;
            bool radialMatches = Math.Abs(actualRadial - tracked.PlannedRadial) <= DeltaVToleranceMetersPerSecond;
            bool verified = utMatches && progradeMatches && normalMatches && radialMatches;

            if (!tracked.TransferAssessmentAvailable ||
                !IsFinite(tracked.AssessedNodeUt) ||
                Math.Abs(actualUt - tracked.AssessedNodeUt) > NodeUtToleranceSeconds ||
                Math.Abs(actualPrograde - tracked.AssessedPrograde) > DeltaVToleranceMetersPerSecond ||
                Math.Abs(actualNormal - tracked.AssessedNormal) > DeltaVToleranceMetersPerSecond ||
                Math.Abs(actualRadial - tracked.AssessedRadial) > DeltaVToleranceMetersPerSecond)
            {
                UpdateTransferAssessment(tracked);
            }

            string detail = verified
                ? "KSP NODE MATCHES UPLINKED PLAN"
                : "KSP NODE DIFFERS FROM UPLINKED PLAN";

            SendNodeState(
                tracked,
                verified ? "NODE VERIFIED" : "CREW MODIFIED",
                true,
                actualUt,
                actualPrograde,
                actualNormal,
                actualRadial,
                detail);
        }

        private void UpdateTransferAssessment(TrackedManeuver tracked)
        {
            tracked.TransferAssessmentAvailable = false;
            tracked.TargetEncounter = false;
            tracked.ClosestApproachMeters = double.NaN;
            tracked.ClosestApproachUt = double.NaN;
            tracked.TargetSoiRadiusMeters = double.NaN;

            if (tracked == null || tracked.Node == null ||
                string.IsNullOrWhiteSpace(tracked.TargetBodyName))
                return;

            CelestialBody target = FindBody(tracked.TargetBodyName);
            if (target == null) return;

            tracked.TargetSoiRadiusMeters = target.sphereOfInfluence;

            Orbit patch = tracked.Node.nextPatch;
            HashSet<Orbit> visited = new HashSet<Orbit>();
            double bestDistance = double.PositiveInfinity;
            double bestUt = double.NaN;
            bool encounteredTarget = false;
            int patchCount = 0;

            while (patch != null &&
                   patchCount < AssessmentMaxPatches &&
                   !visited.Contains(patch))
            {
                visited.Add(patch);
                patchCount++;

                if (patch.referenceBody == target)
                    encounteredTarget = true;

                bool comparable =
                    patch.referenceBody == target ||
                    (target.referenceBody != null &&
                     patch.referenceBody == target.referenceBody);

                if (comparable)
                {
                    double start, end;
                    if (TryGetAssessmentInterval(patch, target, tracked.Node.UT, out start, out end))
                    {
                        double localDistance, localUt;
                        if (TryFindClosestApproach(
                                patch, target, start, end,
                                out localDistance, out localUt) &&
                            localDistance < bestDistance)
                        {
                            bestDistance = localDistance;
                            bestUt = localUt;
                        }
                    }
                }

                patch = patch.nextPatch;
            }

            if (!IsFinite(bestDistance) || !IsFinite(bestUt))
                return;

            tracked.TransferAssessmentAvailable = true;
            tracked.TargetEncounter = encounteredTarget;
            tracked.ClosestApproachMeters = bestDistance;
            tracked.ClosestApproachUt = bestUt;
            tracked.AssessedNodeUt = tracked.Node.UT;
            tracked.AssessedRadial = tracked.Node.DeltaV.x;
            tracked.AssessedNormal = tracked.Node.DeltaV.y;
            tracked.AssessedPrograde = tracked.Node.DeltaV.z;

            Debug.Log(
                "[KMC] TRANSFER ASSESSMENT" +
                " | PlanId=" + tracked.PlanId +
                " | Target=" + tracked.TargetBodyName +
                " | Encounter=" + tracked.TargetEncounter +
                " | ClosestApproachM=" + tracked.ClosestApproachMeters.ToString("0.0") +
                " | ClosestUT=" + tracked.ClosestApproachUt.ToString("0.0") +
                " | TargetSOIM=" + tracked.TargetSoiRadiusMeters.ToString("0.0"));
        }

        private static CelestialBody FindBody(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || FlightGlobals.Bodies == null)
                return null;

            for (int i = 0; i < FlightGlobals.Bodies.Count; i++)
            {
                CelestialBody body = FlightGlobals.Bodies[i];
                if (body != null &&
                    string.Equals(body.bodyName, name, StringComparison.OrdinalIgnoreCase))
                    return body;
            }

            return null;
        }

        private static bool TryGetAssessmentInterval(
            Orbit patch,
            CelestialBody target,
            double nodeUt,
            out double start,
            out double end)
        {
            start = double.NaN;
            end = double.NaN;
            if (patch == null || target == null) return false;

            double patchStart = patch.StartUT;
            if (!IsFinite(patchStart)) patchStart = nodeUt;
            start = Math.Max(nodeUt, patchStart);

            double patchEnd = patch.EndUT;
            if (IsFinite(patchEnd) && patchEnd > start)
            {
                end = patchEnd;
            }
            else
            {
                double horizon = double.NaN;

                try
                {
                    if (target.orbit != null &&
                        IsFinite(target.orbit.period) &&
                        target.orbit.period > 0.0)
                        horizon = target.orbit.period * 1.5;
                }
                catch { }

                try
                {
                    if ((!IsFinite(horizon) || horizon <= 0.0) &&
                        IsFinite(patch.period) &&
                        patch.period > 0.0)
                        horizon = patch.period;
                }
                catch { }

                if (!IsFinite(horizon) || horizon <= 0.0)
                    horizon = 20000000.0;

                end = start + horizon;
            }

            return IsFinite(start) && IsFinite(end) && end > start;
        }

        private static bool TryFindClosestApproach(
            Orbit patch,
            CelestialBody target,
            double start,
            double end,
            out double bestDistance,
            out double bestUt)
        {
            bestDistance = double.PositiveInfinity;
            bestUt = double.NaN;

            if (patch == null || target == null || end <= start)
                return false;

            int bestIndex = -1;
            for (int i = 0; i <= AssessmentSamplesPerPatch; i++)
            {
                double fraction = (double)i / AssessmentSamplesPerPatch;
                double ut = start + (end - start) * fraction;
                double distance = DistanceToTarget(patch, target, ut);
                if (IsFinite(distance) && distance < bestDistance)
                {
                    bestDistance = distance;
                    bestUt = ut;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0) return false;

            double step = (end - start) / AssessmentSamplesPerPatch;
            double left = Math.Max(start, bestUt - step);
            double right = Math.Min(end, bestUt + step);

            for (int i = 0; i < AssessmentRefineIterations; i++)
            {
                double m1 = left + (right - left) / 3.0;
                double m2 = right - (right - left) / 3.0;
                double d1 = DistanceToTarget(patch, target, m1);
                double d2 = DistanceToTarget(patch, target, m2);

                if (!IsFinite(d1) || !IsFinite(d2)) break;

                if (d1 <= d2)
                    right = m2;
                else
                    left = m1;
            }

            double refinedUt = 0.5 * (left + right);
            double refinedDistance = DistanceToTarget(patch, target, refinedUt);
            if (IsFinite(refinedDistance) && refinedDistance < bestDistance)
            {
                bestDistance = refinedDistance;
                bestUt = refinedUt;
            }

            return IsFinite(bestDistance) && IsFinite(bestUt);
        }

        private static double DistanceToTarget(
            Orbit patch,
            CelestialBody target,
            double ut)
        {
            try
            {
                Vector3d vessel = patch.getRelativePositionAtUT(ut);

                if (patch.referenceBody == target)
                    return vessel.magnitude;

                if (target.referenceBody != null &&
                    patch.referenceBody == target.referenceBody &&
                    target.orbit != null)
                {
                    Vector3d targetPosition =
                        target.orbit.getRelativePositionAtUT(ut);

                    return (vessel - targetPosition).magnitude;
                }
            }
            catch
            {
            }

            return double.NaN;
        }

        private void SendNodeState(
            TrackedManeuver tracked,
            string state,
            bool nodeExists,
            double nodeUt,
            double prograde,
            double normal,
            double radial,
            string detail)
        {
            if (_stateClient == null || _stateEndpoint == null || tracked == null) return;

            try
            {
                ManeuverNodeStatePacket packet = new ManeuverNodeStatePacket
                {
                    VesselId = tracked.VesselId ?? string.Empty,
                    PlanId = tracked.PlanId ?? string.Empty,
                    State = state ?? string.Empty,
                    NodeExists = nodeExists,
                    NodeUniversalTimeSeconds = nodeUt,
                    ProgradeDeltaVMetersPerSecond = prograde,
                    NormalDeltaVMetersPerSecond = normal,
                    RadialDeltaVMetersPerSecond = radial,
                    Detail = detail ?? string.Empty,
                    TargetBodyName = tracked.TargetBodyName ?? string.Empty,
                    TransferAssessmentAvailable = tracked.TransferAssessmentAvailable,
                    TargetEncounter = tracked.TargetEncounter,
                    ClosestApproachMeters = tracked.ClosestApproachMeters,
                    ClosestApproachUniversalTimeSeconds = tracked.ClosestApproachUt,
                    TargetSoiRadiusMeters = tracked.TargetSoiRadiusMeters
                };

                byte[] data = Encoding.UTF8.GetBytes(packet.Serialize());
                _stateClient.Send(data, data.Length, _stateEndpoint);
            }
            catch (Exception ex)
            {
                Debug.LogError("[KMC] Maneuver node-state send failed: " + ex);
            }
        }

        private void SendAck(
            ManeuverUplinkPacket packet,
            string status,
            string detail,
            double nodeUt)
        {
            if (_ackClient == null || _ackEndpoint == null || packet == null) return;

            try
            {
                ManeuverUplinkAck ack = new ManeuverUplinkAck
                {
                    VesselId = packet.VesselId ?? string.Empty,
                    PlanId = packet.PlanId ?? string.Empty,
                    Status = status ?? string.Empty,
                    NodeUniversalTimeSeconds = nodeUt,
                    Detail = detail ?? string.Empty
                };

                byte[] data = Encoding.UTF8.GetBytes(ack.Serialize());
                _ackClient.Send(data, data.Length, _ackEndpoint);
            }
            catch (Exception ex)
            {
                Debug.LogError("[KMC] Maneuver ACK send failed: " + ex);
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        public void OnDestroy()
        {
            _running = false;
            if (_receiveClient != null) { _receiveClient.Close(); _receiveClient = null; }
            if (_ackClient != null) { _ackClient.Close(); _ackClient = null; }
            if (_stateClient != null) { _stateClient.Close(); _stateClient = null; }

            if (_receiveThread != null && _receiveThread.IsAlive)
                _receiveThread.Join(250);

            _receiveThread = null;
        }
    }
}
