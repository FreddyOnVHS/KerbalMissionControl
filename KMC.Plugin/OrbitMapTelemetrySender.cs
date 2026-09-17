using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using KMC.Shared;
using UnityEngine;

namespace KMC.Plugin
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class OrbitMapTelemetrySender : MonoBehaviour
    {
        private const float SendIntervalSeconds = 0.2f;
        private UdpClient _udpClient;
        private IPEndPoint _endpoint;
        private float _nextSendTime;
        private long _sequence;

        public void Start()
        {
            try
            {
                _udpClient = new UdpClient();
                _endpoint = new IPEndPoint(IPAddress.Loopback, OrbitMapPacket.TelemetryPort);
                Debug.Log("[KMC] Orbit map telemetry started.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[KMC] Orbit map telemetry start failed: " + ex);
            }
        }

        public void Update()
        {
            if (_udpClient == null || Time.realtimeSinceStartup < _nextSendTime) return;
            _nextSendTime = Time.realtimeSinceStartup + SendIntervalSeconds;

            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel == null || vessel.orbit == null || vessel.mainBody == null) return;

            try
            {
                double ut = Planetarium.GetUniversalTime();
                OrbitMapPacket packet = new OrbitMapPacket();
                packet.Sequence = ++_sequence;
                packet.TimestampUtc = DateTime.UtcNow;
                packet.VesselId = vessel.id.ToString();
                packet.VesselName = vessel.vesselName ?? string.Empty;
                packet.UniversalTimeSeconds = ut;
                packet.ReferenceBodyName = vessel.mainBody.bodyName ?? string.Empty;
                packet.ReferenceBodyRadiusMeters = vessel.mainBody.Radius;
                packet.ReferenceBodySoiRadiusMeters = double.IsNaN(vessel.mainBody.sphereOfInfluence) || double.IsInfinity(vessel.mainBody.sphereOfInfluence) ? 0.0 : vessel.mainBody.sphereOfInfluence;
                packet.ActiveOrbit = BuildOrbit(vessel.orbit, CanonicalPositionAtTrueAnomaly(vessel.orbit, vessel.orbit.trueAnomaly));
                packet.Target = BuildTarget(vessel, ut);
                BuildNodes(vessel, packet, ut);
                BuildPatches(vessel, packet);

                byte[] data = Encoding.UTF8.GetBytes(packet.Serialize());
                _udpClient.Send(data, data.Length, _endpoint);
            }
            catch (Exception ex)
            {
                Debug.LogError("[KMC] Orbit map telemetry send failed: " + ex);
            }
        }

        private static OrbitMapOrbit BuildOrbit(Orbit orbit, Vector3d position)
        {
            if (orbit == null) return null;
            OrbitMapOrbit result = new OrbitMapOrbit();
            result.ReferenceBodyName = orbit.referenceBody != null ? orbit.referenceBody.bodyName : string.Empty;
            result.SemiMajorAxisMeters = orbit.semiMajorAxis;
            result.Eccentricity = orbit.eccentricity;
            result.InclinationDegrees = orbit.inclination;
            result.LongitudeOfAscendingNodeDegrees = orbit.LAN;
            result.ArgumentOfPeriapsisDegrees = orbit.argumentOfPeriapsis;
            result.EpochUniversalTimeSeconds = orbit.epoch;
            result.MeanAnomalyAtEpochRadians = orbit.meanAnomalyAtEpoch;
            double referenceRadius = orbit.referenceBody != null ? orbit.referenceBody.Radius : 0.0;
            result.ApoapsisMeters = orbit.eccentricity < 1.0
                ? (orbit.semiMajorAxis * (1.0 + orbit.eccentricity)) - referenceRadius
                : double.NaN;
            result.PeriapsisMeters = (orbit.semiMajorAxis * (1.0 - orbit.eccentricity)) - referenceRadius;

            // Do not use Orbit.period here. Future patched-conic Orbit instances can
            // be only partially initialized. Derive elliptic period directly from
            // semi-major axis and the reference body's gravitational parameter.
            result.PeriodSeconds = double.NaN;
            if (orbit.eccentricity < 1.0 && orbit.semiMajorAxis > 0.0 && orbit.referenceBody != null)
            {
                double mu = orbit.referenceBody.gravParameter;
                if (mu > 0.0 && !double.IsNaN(mu) && !double.IsInfinity(mu))
                    result.PeriodSeconds = 2.0 * Math.PI * Math.Sqrt((orbit.semiMajorAxis * orbit.semiMajorAxis * orbit.semiMajorAxis) / mu);
            }
            result.PositionX = position.x;
            result.PositionY = position.y;
            result.PositionZ = position.z;
            return result;
        }

        private static Vector3d CanonicalPositionAtTrueAnomaly(Orbit orbit, double trueAnomalyRadians)
        {
            if (orbit == null) return new Vector3d(0.0, 0.0, 0.0);

            // This intentionally mirrors OrbitMapConicSampler.PositionAtTrueAnomaly()
            // in Mission Control. KSP's native relative-position vector uses a
            // different celestial coordinate convention; mixing that vector with
            // KMC's reconstructed conic is what displaced the VSL marker.
            double a = orbit.semiMajorAxis;
            double e = orbit.eccentricity;
            double p = a * (1.0 - e * e);
            double denom = 1.0 + e * Math.Cos(trueAnomalyRadians);
            if (Math.Abs(denom) < 1e-12) denom = denom < 0.0 ? -1e-12 : 1e-12;
            double r = p / denom;
            double x = r * Math.Cos(trueAnomalyRadians);
            double y = r * Math.Sin(trueAnomalyRadians);

            double w = orbit.argumentOfPeriapsis * Math.PI / 180.0;
            double inc = orbit.inclination * Math.PI / 180.0;
            double lan = orbit.LAN * Math.PI / 180.0;

            double cw = Math.Cos(w), sw = Math.Sin(w);
            double ci = Math.Cos(inc), si = Math.Sin(inc);
            double co = Math.Cos(lan), so = Math.Sin(lan);

            double x1 = cw * x - sw * y;
            double y1 = sw * x + cw * y;
            double x2 = x1;
            double y2 = ci * y1;
            double z2 = si * y1;
            return new Vector3d(co * x2 - so * y2, so * x2 + co * y2, z2);
        }

        private static OrbitMapTarget BuildTarget(Vessel active, double ut)
        {
            OrbitMapTarget result = new OrbitMapTarget();
            if (FlightGlobals.fetch == null || FlightGlobals.fetch.VesselTarget == null) return result;

            try
            {
                ITargetable target = FlightGlobals.fetch.VesselTarget;
                Vessel targetVessel = target.GetVessel();
                Orbit targetOrbit = targetVessel != null ? targetVessel.orbit : target.GetOrbit();
                if (targetOrbit == null) return result;

                result.Present = true;
                result.TargetType = targetVessel != null ? "VESSEL" : "BODY";
                result.TargetId = targetVessel != null ? targetVessel.id.ToString() : target.GetName();
                result.TargetName = target.GetName() ?? string.Empty;
                Vector3d position = CanonicalPositionAtTrueAnomaly(targetOrbit, targetOrbit.trueAnomaly);
                result.Orbit = BuildOrbit(targetOrbit, position);
                result.PositionX = position.x;
                result.PositionY = position.y;
                result.PositionZ = position.z;
            }
            catch
            {
                return new OrbitMapTarget();
            }
            return result;
        }

        private static void BuildNodes(Vessel active, OrbitMapPacket packet, double ut)
        {
            if (active.patchedConicSolver == null || active.patchedConicSolver.maneuverNodes == null) return;
            List<ManeuverNode> nodes = new List<ManeuverNode>(active.patchedConicSolver.maneuverNodes);
            nodes.Sort(delegate(ManeuverNode a, ManeuverNode b) { return a.UT.CompareTo(b.UT); });
            int count = Math.Min(nodes.Count, OrbitMapPacket.MaxManeuverNodes);
            for (int i = 0; i < count; i++)
            {
                ManeuverNode node = nodes[i];
                Vector3d dv = node.DeltaV;
                double nodeTrueAnomaly = TrueAnomalyAtUT(active.orbit, node.UT);
                Vector3d position = CanonicalPositionAtTrueAnomaly(active.orbit, nodeTrueAnomaly);
                OrbitMapManeuverNode item = new OrbitMapManeuverNode();
                item.Index = i;
                item.UniversalTimeSeconds = node.UT;
                item.ProgradeDeltaVMetersPerSecond = dv.z;
                item.NormalDeltaVMetersPerSecond = dv.y;
                item.RadialDeltaVMetersPerSecond = dv.x;
                item.TotalDeltaVMetersPerSecond = dv.magnitude;
                item.PositionX = position.x;
                item.PositionY = position.y;
                item.PositionZ = position.z;
                item.PatchIndex = 0;
                packet.ManeuverNodes.Add(item);
            }
        }

        private static double TrueAnomalyAtUT(Orbit orbit, double ut)
        {
            if (orbit == null || orbit.referenceBody == null) return 0.0;

            double e = orbit.eccentricity;
            double a = orbit.semiMajorAxis;
            double mu = orbit.referenceBody.gravParameter;
            if (mu <= 0.0 || a == 0.0 || double.IsNaN(a) || double.IsInfinity(a)) return orbit.trueAnomaly;

            double meanMotion = Math.Sqrt(mu / Math.Pow(Math.Abs(a), 3.0));
            double meanAnomaly = orbit.meanAnomalyAtEpoch + meanMotion * (ut - orbit.epoch);

            if (e < 1.0)
            {
                meanAnomaly = NormalizeRadians(meanAnomaly);
                double eccentricAnomaly = SolveEccentricAnomaly(meanAnomaly, e);
                double sinHalf = Math.Sqrt(1.0 + e) * Math.Sin(eccentricAnomaly * 0.5);
                double cosHalf = Math.Sqrt(1.0 - e) * Math.Cos(eccentricAnomaly * 0.5);
                return NormalizeRadians(2.0 * Math.Atan2(sinHalf, cosHalf));
            }

            if (e > 1.0)
            {
                double hyperbolicAnomaly = SolveHyperbolicAnomaly(meanAnomaly, e);
                double factor = Math.Sqrt((e + 1.0) / (e - 1.0));
                return 2.0 * Math.Atan(factor * Math.Tanh(hyperbolicAnomaly * 0.5));
            }

            return orbit.trueAnomaly;
        }

        private static double SolveEccentricAnomaly(double meanAnomaly, double eccentricity)
        {
            double value = eccentricity < 0.8 ? meanAnomaly : Math.PI;
            for (int i = 0; i < 12; i++)
            {
                double f = value - eccentricity * Math.Sin(value) - meanAnomaly;
                double fp = 1.0 - eccentricity * Math.Cos(value);
                if (Math.Abs(fp) < 1e-12) break;
                double delta = f / fp;
                value -= delta;
                if (Math.Abs(delta) < 1e-12) break;
            }
            return value;
        }

        private static double SolveHyperbolicAnomaly(double meanAnomaly, double eccentricity)
        {
            double value = Math.Log((2.0 * Math.Abs(meanAnomaly) / eccentricity) + 1.8);
            if (meanAnomaly < 0.0) value = -value;

            for (int i = 0; i < 16; i++)
            {
                double sinh = Math.Sinh(value);
                double cosh = Math.Cosh(value);
                double f = eccentricity * sinh - value - meanAnomaly;
                double fp = eccentricity * cosh - 1.0;
                if (Math.Abs(fp) < 1e-12) break;
                double delta = f / fp;
                value -= delta;
                if (Math.Abs(delta) < 1e-12) break;
            }
            return value;
        }

        private static double NormalizeRadians(double radians)
        {
            double twoPi = 2.0 * Math.PI;
            radians %= twoPi;
            if (radians < 0.0) radians += twoPi;
            return radians;
        }

        private static void BuildPatches(Vessel active, OrbitMapPacket packet)
        {
            Orbit patch = null;

            // KSP's active.orbit/nextPatch chain represents the vessel's current
            // momentum only. ManeuverNode.nextPatch is KSP's authoritative
            // projected post-burn orbit. Start there when a node exists so the
            // MAP page reproduces the white projected trajectory from KSP Map View.
            if (active.patchedConicSolver != null && active.patchedConicSolver.maneuverNodes != null &&
                active.patchedConicSolver.maneuverNodes.Count > 0)
            {
                List<ManeuverNode> nodes = new List<ManeuverNode>(active.patchedConicSolver.maneuverNodes);
                nodes.Sort(delegate(ManeuverNode a, ManeuverNode b) { return a.UT.CompareTo(b.UT); });
                ManeuverNode firstNode = nodes[0];
                patch = firstNode.nextPatch;
            }
            else if (active.orbit != null)
            {
                // No planned burn: only send a genuinely future natural SOI patch.
                // The current orbit is already transmitted separately as ActiveOrbit.
                patch = active.orbit.nextPatch;
            }

            HashSet<Orbit> visited = new HashSet<Orbit>();
            int index = 0;
            while (patch != null && index < OrbitMapPacket.MaxPatches && !visited.Contains(patch))
            {
                visited.Add(patch);
                double startUt = patch.StartUT;
                double endUt = patch.EndUT;
                // Patch display geometry is reconstructed from orbital elements in
                // Mission Control. Do not ask KSP to propagate a patch at StartUT
                // here: KSP can expose partially initialized future/landed patches
                // whose propagation path throws inside Orbit.get_semiLatusRectum().
                Vector3d startPosition = new Vector3d(0.0, 0.0, 0.0);
                OrbitMapPatch item = new OrbitMapPatch();
                item.Index = index;
                item.StartUniversalTimeSeconds = startUt;
                item.EndUniversalTimeSeconds = endUt;
                item.TransitionType = patch.patchEndTransition.ToString();
                item.NextBodyName = patch.nextPatch != null && patch.nextPatch.referenceBody != null ? patch.nextPatch.referenceBody.bodyName : string.Empty;
                item.Orbit = BuildOrbit(patch, startPosition);
                packet.Patches.Add(item);
                patch = patch.nextPatch;
                index++;
            }
        }

        public void OnDestroy()
        {
            if (_udpClient != null)
            {
                _udpClient.Close();
                _udpClient = null;
            }
        }
    }
}
