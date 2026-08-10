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
    /// <summary>
    /// Receives reviewed Mission Control maneuver plans, creates stock KSP
    /// maneuver nodes, and reports the live KSP node state back to Mission
    /// Control for Build 11.3 verification / synchronization.
    ///
    /// All KSP flight-state access remains on Unity's Update() thread.
    /// </summary>
    [KSPAddon(
        KSPAddon.Startup.Flight,
        false)]
    public sealed class ManeuverUplinkReceiver :
        MonoBehaviour
    {
        private const float StateSendIntervalSeconds =
            0.25f;

        private const double NodeUtToleranceSeconds =
            0.25;

        private const double DeltaVToleranceMetersPerSecond =
            0.05;

        private sealed class TrackedManeuver
        {
            public string VesselId;
            public string PlanId;
            public ManeuverNode Node;

            public double PlannedNodeUt;
            public double PlannedPrograde;
            public double PlannedNormal;
            public double PlannedRadial;
        }

        private readonly object _syncRoot =
            new object();

        private readonly Dictionary<string, TrackedManeuver>
            _trackedPlans =
                new Dictionary<string, TrackedManeuver>(
                    StringComparer.Ordinal);

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
                _receiveClient =
                    new UdpClient(
                        new IPEndPoint(
                            IPAddress.Loopback,
                            ManeuverUplinkPacket.CommandPort));

                _ackClient =
                    new UdpClient();

                _stateClient =
                    new UdpClient();

                _ackEndpoint =
                    new IPEndPoint(
                        IPAddress.Loopback,
                        ManeuverUplinkPacket.AckPort);

                _stateEndpoint =
                    new IPEndPoint(
                        IPAddress.Loopback,
                        ManeuverUplinkPacket.NodeStatePort);

                _running = true;

                _receiveThread =
                    new Thread(
                        ReceiveLoop);

                _receiveThread.IsBackground =
                    true;

                _receiveThread.Name =
                    "KMC Maneuver Uplink";

                _receiveThread.Start();

                Debug.Log(
                    "[KMC] Maneuver uplink / verification receiver started.");
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[KMC] Maneuver uplink receiver start failed: " +
                    ex);
            }
        }

        public void Update()
        {
            ManeuverUplinkPacket packet =
                null;

            lock (_syncRoot)
            {
                if (_pending != null)
                {
                    packet =
                        _pending;

                    _pending =
                        null;
                }
            }

            if (packet != null)
            {
                ApplyManeuver(
                    packet);
            }

            if (Time.realtimeSinceStartup >=
                _nextStateSendTime)
            {
                _nextStateSendTime =
                    Time.realtimeSinceStartup +
                    StateSendIntervalSeconds;

                PublishTrackedNodeStates();
            }
        }

        private void ReceiveLoop()
        {
            while (_running)
            {
                try
                {
                    IPEndPoint sender =
                        new IPEndPoint(
                            IPAddress.Any,
                            0);

                    byte[] data =
                        _receiveClient.Receive(
                            ref sender);

                    string text =
                        Encoding.UTF8.GetString(
                            data);

                    ManeuverUplinkPacket packet;

                    if (!ManeuverUplinkPacket.TryParse(
                            text,
                            out packet))
                    {
                        continue;
                    }

                    lock (_syncRoot)
                    {
                        _pending =
                            packet;
                    }
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException)
                {
                    if (!_running)
                    {
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError(
                        "[KMC] Maneuver uplink receive failed: " +
                        ex);
                }
            }
        }

        private void ApplyManeuver(
            ManeuverUplinkPacket packet)
        {
            Vessel vessel =
                FlightGlobals.ActiveVessel;

            if (vessel == null)
            {
                SendAck(
                    packet,
                    "REJECTED",
                    "NO ACTIVE VESSEL",
                    double.NaN);

                return;
            }

            string activeVesselId =
                vessel.id.ToString();

            if (!string.Equals(
                    activeVesselId,
                    packet.VesselId,
                    StringComparison.Ordinal))
            {
                SendAck(
                    packet,
                    "REJECTED",
                    "ACTIVE VESSEL ID DOES NOT MATCH PLAN",
                    double.NaN);

                return;
            }

            TrackedManeuver existing;

            if (_trackedPlans.TryGetValue(
                    packet.PlanId,
                    out existing))
            {
                SendAck(
                    packet,
                    "NODE LOADED",
                    "PLAN ALREADY TRACKED - DUPLICATE SUPPRESSED",
                    existing.PlannedNodeUt);

                return;
            }

            double currentUt =
                Planetarium.GetUniversalTime();

            if (!IsFinite(
                    packet.NodeUniversalTimeSeconds) ||
                packet.NodeUniversalTimeSeconds <=
                    currentUt + 0.25)
            {
                SendAck(
                    packet,
                    "REJECTED",
                    "NODE UT IS NOT IN THE FUTURE",
                    double.NaN);

                return;
            }

            if (!IsFinite(
                    packet.ProgradeDeltaVMetersPerSecond) ||
                !IsFinite(
                    packet.NormalDeltaVMetersPerSecond) ||
                !IsFinite(
                    packet.RadialDeltaVMetersPerSecond))
            {
                SendAck(
                    packet,
                    "REJECTED",
                    "MANEUVER DELTA-V IS INVALID",
                    double.NaN);

                return;
            }

            if (vessel.patchedConicSolver == null)
            {
                SendAck(
                    packet,
                    "REJECTED",
                    "PATCHED CONIC SOLVER UNAVAILABLE",
                    double.NaN);

                return;
            }

            try
            {
                ManeuverNode node =
                    vessel.patchedConicSolver
                        .AddManeuverNode(
                            packet.NodeUniversalTimeSeconds);

                if (node == null)
                {
                    SendAck(
                        packet,
                        "REJECTED",
                        "KSP DID NOT CREATE A MANEUVER NODE",
                        double.NaN);

                    return;
                }

                /*
                 * KSP ManeuverNode coordinates:
                 * X = radial-out, Y = normal, Z = prograde.
                 */
                node.DeltaV =
                    new Vector3d(
                        packet.RadialDeltaVMetersPerSecond,
                        packet.NormalDeltaVMetersPerSecond,
                        packet.ProgradeDeltaVMetersPerSecond);

                vessel.patchedConicSolver
                    .UpdateFlightPlan();

                _trackedPlans.Add(
                    packet.PlanId,
                    new TrackedManeuver
                    {
                        VesselId =
                            packet.VesselId,

                        PlanId =
                            packet.PlanId,

                        Node =
                            node,

                        PlannedNodeUt =
                            packet.NodeUniversalTimeSeconds,

                        PlannedPrograde =
                            packet.ProgradeDeltaVMetersPerSecond,

                        PlannedNormal =
                            packet.NormalDeltaVMetersPerSecond,

                        PlannedRadial =
                            packet.RadialDeltaVMetersPerSecond
                    });

                SendAck(
                    packet,
                    "NODE LOADED",
                    "PLUGIN CREATED MANEUVER NODE",
                    packet.NodeUniversalTimeSeconds);

                /*
                 * Publish immediately as well as on the periodic cycle so
                 * Mission Control can transition from NODE LOADED to
                 * NODE VERIFIED without waiting for the next interval.
                 */
                PublishTrackedNodeState(
                    vessel,
                    _trackedPlans[packet.PlanId]);

                ScreenMessages.PostScreenMessage(
                    "KMC maneuver node loaded",
                    4f,
                    ScreenMessageStyle.UPPER_CENTER);

                Debug.Log(
                    "[KMC] MANEUVER NODE LOADED" +
                    " | PlanId=" +
                    packet.PlanId +
                    " | VesselId=" +
                    packet.VesselId +
                    " | NodeUT=" +
                    packet.NodeUniversalTimeSeconds.ToString("0.0") +
                    " | ProgradeDV=" +
                    packet.ProgradeDeltaVMetersPerSecond.ToString("0.00") +
                    " | NormalDV=" +
                    packet.NormalDeltaVMetersPerSecond.ToString("0.00") +
                    " | RadialDV=" +
                    packet.RadialDeltaVMetersPerSecond.ToString("0.00"));
            }
            catch (Exception ex)
            {
                SendAck(
                    packet,
                    "REJECTED",
                    "KSP NODE CREATION FAILED: " +
                    ex.GetType().Name,
                    double.NaN);

                Debug.LogError(
                    "[KMC] Maneuver node creation failed: " +
                    ex);
            }
        }

        private void PublishTrackedNodeStates()
        {
            if (_trackedPlans.Count == 0)
            {
                return;
            }

            Vessel activeVessel =
                FlightGlobals.ActiveVessel;

            foreach (
                KeyValuePair<string, TrackedManeuver> pair
                in _trackedPlans)
            {
                PublishTrackedNodeState(
                    activeVessel,
                    pair.Value);
            }
        }

        private void PublishTrackedNodeState(
            Vessel activeVessel,
            TrackedManeuver tracked)
        {
            if (tracked == null)
            {
                return;
            }

            if (activeVessel == null ||
                !string.Equals(
                    activeVessel.id.ToString(),
                    tracked.VesselId,
                    StringComparison.Ordinal))
            {
                SendNodeState(
                    tracked,
                    "VESSEL NOT ACTIVE",
                    false,
                    double.NaN,
                    double.NaN,
                    double.NaN,
                    double.NaN,
                    "TRACKED MANEUVER VESSEL IS NOT THE ACTIVE VESSEL");

                return;
            }

            PatchedConicSolver solver =
                activeVessel.patchedConicSolver;

            if (solver == null ||
                solver.maneuverNodes == null ||
                tracked.Node == null ||
                !solver.maneuverNodes.Contains(
                    tracked.Node))
            {
                SendNodeState(
                    tracked,
                    "NODE REMOVED",
                    false,
                    double.NaN,
                    double.NaN,
                    double.NaN,
                    double.NaN,
                    "TRACKED KSP MANEUVER NODE NO LONGER EXISTS");

                return;
            }

            double actualUt =
                tracked.Node.UT;

            Vector3d actualDeltaV =
                tracked.Node.DeltaV;

            double actualRadial =
                actualDeltaV.x;

            double actualNormal =
                actualDeltaV.y;

            double actualPrograde =
                actualDeltaV.z;

            bool utMatches =
                Math.Abs(
                    actualUt -
                    tracked.PlannedNodeUt) <=
                NodeUtToleranceSeconds;

            bool progradeMatches =
                Math.Abs(
                    actualPrograde -
                    tracked.PlannedPrograde) <=
                DeltaVToleranceMetersPerSecond;

            bool normalMatches =
                Math.Abs(
                    actualNormal -
                    tracked.PlannedNormal) <=
                DeltaVToleranceMetersPerSecond;

            bool radialMatches =
                Math.Abs(
                    actualRadial -
                    tracked.PlannedRadial) <=
                DeltaVToleranceMetersPerSecond;

            bool verified =
                utMatches &&
                progradeMatches &&
                normalMatches &&
                radialMatches;

            SendNodeState(
                tracked,
                verified
                    ? "NODE VERIFIED"
                    : "CREW MODIFIED",
                true,
                actualUt,
                actualPrograde,
                actualNormal,
                actualRadial,
                verified
                    ? "KSP NODE MATCHES UPLINKED PLAN"
                    : "KSP NODE DIFFERS FROM UPLINKED PLAN");
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
            if (_stateClient == null ||
                _stateEndpoint == null ||
                tracked == null)
            {
                return;
            }

            try
            {
                ManeuverNodeStatePacket packet =
                    new ManeuverNodeStatePacket
                    {
                        VesselId =
                            tracked.VesselId ?? string.Empty,

                        PlanId =
                            tracked.PlanId ?? string.Empty,

                        State =
                            state ?? string.Empty,

                        NodeExists =
                            nodeExists,

                        NodeUniversalTimeSeconds =
                            nodeUt,

                        ProgradeDeltaVMetersPerSecond =
                            prograde,

                        NormalDeltaVMetersPerSecond =
                            normal,

                        RadialDeltaVMetersPerSecond =
                            radial,

                        Detail =
                            detail ?? string.Empty
                    };

                byte[] data =
                    Encoding.UTF8.GetBytes(
                        packet.Serialize());

                _stateClient.Send(
                    data,
                    data.Length,
                    _stateEndpoint);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[KMC] Maneuver node-state send failed: " +
                    ex);
            }
        }

        private void SendAck(
            ManeuverUplinkPacket packet,
            string status,
            string detail,
            double nodeUt)
        {
            if (_ackClient == null ||
                _ackEndpoint == null ||
                packet == null)
            {
                return;
            }

            try
            {
                ManeuverUplinkAck ack =
                    new ManeuverUplinkAck
                    {
                        VesselId =
                            packet.VesselId ??
                            string.Empty,

                        PlanId =
                            packet.PlanId ??
                            string.Empty,

                        Status =
                            status ??
                            string.Empty,

                        NodeUniversalTimeSeconds =
                            nodeUt,

                        Detail =
                            detail ??
                            string.Empty
                    };

                byte[] data =
                    Encoding.UTF8.GetBytes(
                        ack.Serialize());

                _ackClient.Send(
                    data,
                    data.Length,
                    _ackEndpoint);
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[KMC] Maneuver ACK send failed: " +
                    ex);
            }
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }

        public void OnDestroy()
        {
            _running =
                false;

            if (_receiveClient != null)
            {
                _receiveClient.Close();
                _receiveClient = null;
            }

            if (_ackClient != null)
            {
                _ackClient.Close();
                _ackClient = null;
            }

            if (_stateClient != null)
            {
                _stateClient.Close();
                _stateClient = null;
            }

            if (_receiveThread != null &&
                _receiveThread.IsAlive)
            {
                _receiveThread.Join(
                    250);
            }

            _receiveThread =
                null;
        }
    }
}
