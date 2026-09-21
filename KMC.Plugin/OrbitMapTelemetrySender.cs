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
                BuildBodies(vessel, packet, ut);
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

        private static void BuildBodies(Vessel vessel, OrbitMapPacket packet, double ut)
        {
            CelestialBody primary = vessel.mainBody;
            if (primary == null) return;

            OrbitMapBody root = new OrbitMapBody();
            root.Name = primary.bodyName ?? string.Empty;
            root.ParentName = primary.referenceBody != null && primary.referenceBody != primary ? primary.referenceBody.bodyName ?? string.Empty : string.Empty;
            root.RadiusMeters = primary.Radius;
            root.SoiRadiusMeters = IsFinite(primary.sphereOfInfluence) ? primary.sphereOfInfluence : 0.0;
            root.GravParameter = primary.gravParameter;
            root.PositionX = 0.0;
            root.PositionY = 0.0;
            root.PositionZ = 0.0;
            packet.Bodies.Add(root);

            if (FlightGlobals.Bodies == null) return;
            for (int i = 0; i < FlightGlobals.Bodies.Count; i++)
            {
                if (packet.Bodies.Count >= OrbitMapPacket.MaxBodies) break;
                CelestialBody candidate = FlightGlobals.Bodies[i];
                if (candidate == null || candidate == primary || candidate.referenceBody != primary || candidate.orbit == null) continue;

                Vector3d position = CanonicalPositionAtTrueAnomaly(candidate.orbit, candidate.orbit.trueAnomaly);
                OrbitMapBody body = new OrbitMapBody();
                body.Name = candidate.bodyName ?? string.Empty;
                body.ParentName = primary.bodyName ?? string.Empty;
                body.RadiusMeters = candidate.Radius;
                body.SoiRadiusMeters = IsFinite(candidate.sphereOfInfluence) ? candidate.sphereOfInfluence : 0.0;
                body.GravParameter = candidate.gravParameter;
                body.Orbit = BuildOrbit(candidate.orbit, position);
                body.PositionX = position.x;
                body.PositionY = position.y;
                body.PositionZ = position.z;
                packet.Bodies.Add(body);
            }
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
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
            int remainingAuthoritativeSamples = OrbitMapPacket.MaxTotalPatchSamples;
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
                if (!IsSerializablePatchOrbit(item.Orbit))
                {
                    Debug.LogWarning("[KMC] Orbit map skipped invalid future patch " + index +
                        " (" + (item.Orbit != null ? item.Orbit.ReferenceBodyName : "null") +
                        ") because KSP exposed non-finite orbital elements.");
                    break;
                }

                // Cross-SOI geometry is sampled by KSP itself. A single frame
                // calibration built from the active orbit maps KSP BCI/world axes
                // into KMC's established canonical renderer for every patch/body.
                BuildAuthoritativePatchSamples(patch, item, packet.ReferenceBodyName, ref remainingAuthoritativeSamples);
                packet.Patches.Add(item);
                patch = patch.nextPatch;
                index++;
            }
        }


        private static void BuildAuthoritativePatchSamples(Orbit patch, OrbitMapPatch item, string primaryBodyName, ref int remainingSamples)
        {
            if (patch == null || item == null || patch.referenceBody == null || remainingSamples < 4) return;

            string referenceName = patch.referenceBody.bodyName ?? string.Empty;
            CelestialBody referenceBody = patch.referenceBody;
            bool primaryReferenced = string.Equals(referenceName, primaryBodyName ?? string.Empty, StringComparison.OrdinalIgnoreCase);

            if (!primaryReferenced &&
                (referenceBody.orbit == null || referenceBody.referenceBody == null ||
                 !string.Equals(referenceBody.referenceBody.bodyName ?? string.Empty,
                     primaryBodyName ?? string.Empty, StringComparison.OrdinalIgnoreCase)))
                return;

            double start = patch.StartUT;
            double end = patch.EndUT;
            if (!IsFinite(start) || !IsFinite(end) || end <= start) return;

            try
            {
                int sampleCount = Math.Min(OrbitMapPacket.MaxPatchSamples, remainingSamples);
                double span = end - start;
                double boundaryInset = Math.Min(0.001, span * 1e-6);
                double firstUt = start + boundaryInset;
                double lastUt = end - boundaryInset;
                if (lastUt <= firstUt)
                {
                    firstUt = start;
                    lastUt = end;
                }

                List<OrbitMapPatchSample> samples = new List<OrbitMapPatchSample>(sampleCount);
                for (int i = 0; i < sampleCount; i++)
                {
                    double fraction = sampleCount > 1 ? (double)i / (sampleCount - 1) : 0.0;
                    double ut = firstUt + (lastUt - firstUt) * fraction;

                    // KSP is authoritative for patch selection, UT bounds and phase.
                    // KMC canonical coordinates are authoritative for scene orientation.
                    double patchTrueAnomaly = patch.TrueAnomalyAtT(patch.getObtAtUT(ut));
                    Vector3d canonicalLocal = CanonicalPositionAtTrueAnomaly(patch, patchTrueAnomaly);
                    Vector3d canonicalParent = canonicalLocal;
                    Vector3d canonicalBody = new Vector3d(0.0, 0.0, 0.0);

                    if (!primaryReferenced && referenceBody.orbit != null)
                    {
                        double bodyTrueAnomaly =
                            referenceBody.orbit.TrueAnomalyAtT(referenceBody.orbit.getObtAtUT(ut));
                        canonicalBody =
                            CanonicalPositionAtTrueAnomaly(referenceBody.orbit, bodyTrueAnomaly);
                        canonicalParent = canonicalLocal + canonicalBody;
                    }

                    if (!IsFinite(canonicalParent.x) || !IsFinite(canonicalParent.y) || !IsFinite(canonicalParent.z) ||
                        !IsFinite(canonicalBody.x) || !IsFinite(canonicalBody.y) || !IsFinite(canonicalBody.z))
                        throw new InvalidOperationException("KMC canonical projected sample was non-finite.");

                    OrbitMapPatchSample sample = new OrbitMapPatchSample();
                    sample.UniversalTimeSeconds = ut;
                    sample.PositionX = canonicalParent.x;
                    sample.PositionY = canonicalParent.y;
                    sample.PositionZ = canonicalParent.z;
                    sample.ReferenceBodyPositionX = canonicalBody.x;
                    sample.ReferenceBodyPositionY = canonicalBody.y;
                    sample.ReferenceBodyPositionZ = canonicalBody.z;
                    samples.Add(sample);
                }

                LogEncounterDiagnostics(patch, item, primaryBodyName, samples);
                LogEncounterClearanceDiagnostics(patch, item, primaryBodyName, samples);
                for (int i = 0; i < samples.Count; i++) item.Samples.Add(samples[i]);
                remainingSamples -= samples.Count;
            }
            catch (Exception ex)
            {
                item.Samples.Clear();
                Debug.LogWarning("[KMC] Orbit map could not sample canonical projected patch " + item.Index +
                    " (" + referenceName + "): " + ex.GetType().Name + " " + ex.Message);
            }
        }

        private static Vector3d FixedRightHandedPositionAtUT(Orbit orbit, double ut)
        {
            if (orbit == null) return new Vector3d(0.0, 0.0, 0.0);

            double trueAnomaly = orbit.TrueAnomalyAtT(orbit.getObtAtUT(ut));
            Vector3d position;
            Vector3d velocity;
            orbit.GetOrbitalStateVectorsAtTrueAnomaly(trueAnomaly, ut, false, out position, out velocity);
            return Planetarium.Zup.WorldToLocal(position);
        }



        private static void LogEncounterClearanceDiagnostics(Orbit patch, OrbitMapPatch item, string primaryBodyName, List<OrbitMapPatchSample> samples)
        {
            if (patch == null || item == null || patch.referenceBody == null || samples == null || samples.Count == 0) return;

            string referenceName = patch.referenceBody.bodyName ?? string.Empty;
            bool childReferenced = !string.Equals(referenceName, primaryBodyName ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            if (!childReferenced) return;

            try
            {
                double bodyRadius = patch.referenceBody.Radius;
                double expectedPeRadius = double.NaN;
                if (IsFinite(patch.PeA) && IsFinite(bodyRadius))
                    expectedPeRadius = patch.PeA + bodyRadius;

                double minKspLocalRadius = double.PositiveInfinity;
                double minTxRadius = double.PositiveInfinity;
                double minKspUt = double.NaN;
                double minTxUt = double.NaN;

                for (int i = 0; i < samples.Count; i++)
                {
                    OrbitMapPatchSample s = samples[i];
                    double ut = s.UniversalTimeSeconds;

                    Vector3d kspLocal = patch.getRelativePositionAtUT(ut).xzy;
                    double kspRadius = kspLocal.magnitude;
                    if (IsFinite(kspRadius) && kspRadius < minKspLocalRadius)
                    {
                        minKspLocalRadius = kspRadius;
                        minKspUt = ut;
                    }

                    double dx = s.PositionX - s.ReferenceBodyPositionX;
                    double dy = s.PositionY - s.ReferenceBodyPositionY;
                    double dz = s.PositionZ - s.ReferenceBodyPositionZ;
                    double txRadius = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    if (IsFinite(txRadius) && txRadius < minTxRadius)
                    {
                        minTxRadius = txRadius;
                        minTxUt = ut;
                    }
                }

                double clearanceAboveSurface = IsFinite(minTxRadius) && IsFinite(bodyRadius)
                    ? minTxRadius - bodyRadius
                    : double.NaN;

                double expectedPeClearance = IsFinite(expectedPeRadius) && IsFinite(bodyRadius)
                    ? expectedPeRadius - bodyRadius
                    : double.NaN;

                Debug.Log("[KMC-ORBIT-CLEARANCE] patch=" + item.Index +
                    " ref=" + referenceName +
                    " bodyRadius=" + bodyRadius.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    " expectedPeRadius=" + expectedPeRadius.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    " expectedPeClearance=" + expectedPeClearance.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    " minKspLocalRadius=" + minKspLocalRadius.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    " minKspUT=" + minKspUt.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    " minTxRadius=" + minTxRadius.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    " closestUT=" + minTxUt.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    " clearanceAboveSurface=" + clearanceAboveSurface.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    " deltaKspVsTx=" + (minTxRadius - minKspLocalRadius).ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[KMC-ORBIT-CLEARANCE] FAILED patch=" + item.Index + " " +
                    ex.GetType().Name + " " + ex.Message);
            }
        }

        private static void LogEncounterDiagnostics(Orbit patch, OrbitMapPatch item, string primaryBodyName, List<OrbitMapPatchSample> samples)
        {
            if (patch == null || item == null || patch.referenceBody == null || samples == null || samples.Count == 0) return;

            string referenceName = patch.referenceBody.bodyName ?? string.Empty;
            bool childReferenced = !string.Equals(referenceName, primaryBodyName ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            if (!childReferenced) return;

            // Log a compact five-point probe only for child-SOI patches.
            // This is deliberately diagnostic-only and avoids per-frame spam.
            int[] picks = new int[] { 0, samples.Count / 4, samples.Count / 2, (samples.Count * 3) / 4, samples.Count - 1 };

            try
            {
                Debug.Log("[KMC-ORBIT-DIAG] BEGIN patch=" + item.Index +
                    " ref=" + referenceName +
                    " primary=" + (primaryBodyName ?? string.Empty) +
                    " startUT=" + patch.StartUT.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    " endUT=" + patch.EndUT.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    " e=" + patch.eccentricity.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    " sma=" + patch.semiMajorAxis.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    " soi=" + patch.referenceBody.sphereOfInfluence.ToString("R", System.Globalization.CultureInfo.InvariantCulture));

                for (int p = 0; p < picks.Length; p++)
                {
                    int idx = picks[p];
                    if (idx < 0) idx = 0;
                    if (idx >= samples.Count) idx = samples.Count - 1;

                    OrbitMapPatchSample s = samples[idx];
                    double ut = s.UniversalTimeSeconds;

                    Vector3d rawLocal = patch.getRelativePositionAtUT(ut).xzy;
                    Vector3d rawBody = patch.referenceBody.orbit != null
                        ? patch.referenceBody.orbit.getRelativePositionAtUT(ut).xzy
                        : new Vector3d(0.0, 0.0, 0.0);
                    Vector3d rawParent = rawLocal + rawBody;

                    Vector3d fixedLocal = FixedRightHandedPositionAtUT(patch, ut);
                    Vector3d fixedBody = patch.referenceBody.orbit != null
                        ? FixedRightHandedPositionAtUT(patch.referenceBody.orbit, ut)
                        : new Vector3d(0.0, 0.0, 0.0);
                    Vector3d fixedParent = fixedLocal + fixedBody;

                    double patchTrueAnomaly = patch.TrueAnomalyAtT(patch.getObtAtUT(ut));
                    Vector3d canonicalLocal = CanonicalPositionAtTrueAnomaly(patch, patchTrueAnomaly);

                    Vector3d canonicalBody = new Vector3d(0.0, 0.0, 0.0);
                    if (patch.referenceBody.orbit != null)
                    {
                        double bodyTrueAnomaly = patch.referenceBody.orbit.TrueAnomalyAtT(patch.referenceBody.orbit.getObtAtUT(ut));
                        canonicalBody = CanonicalPositionAtTrueAnomaly(patch.referenceBody.orbit, bodyTrueAnomaly);
                    }
                    Vector3d canonicalParent = canonicalLocal + canonicalBody;

                    double localFrameAngleDeg = VectorAngleDegrees(fixedLocal, canonicalLocal);
                    double bodyFrameAngleDeg = VectorAngleDegrees(fixedBody, canonicalBody);
                    double parentFrameAngleDeg = VectorAngleDegrees(fixedParent, canonicalParent);

                    double localRadius = fixedLocal.magnitude;
                    double soi = patch.referenceBody.sphereOfInfluence;
                    double soiError = IsFinite(soi) && soi > 0.0 ? localRadius - soi : double.NaN;

                    Debug.Log("[KMC-ORBIT-DIAG] SAMPLE patch=" + item.Index +
                        " slot=" + p +
                        " i=" + idx +
                        " ut=" + ut.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                        " rawLocal=" + FormatVector(rawLocal) +
                        " rawBody=" + FormatVector(rawBody) +
                        " rawParent=" + FormatVector(rawParent) +
                        " fixedLocal=" + FormatVector(fixedLocal) +
                        " fixedBody=" + FormatVector(fixedBody) +
                        " fixedParent=" + FormatVector(fixedParent) +
                        " canonicalLocal=" + FormatVector(canonicalLocal) +
                        " canonicalBody=" + FormatVector(canonicalBody) +
                        " canonicalParent=" + FormatVector(canonicalParent) +
                        " angleLocalDeg=" + localFrameAngleDeg.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                        " angleBodyDeg=" + bodyFrameAngleDeg.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                        " angleParentDeg=" + parentFrameAngleDeg.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                        " txParent=(" +
                            s.PositionX.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," +
                            s.PositionY.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," +
                            s.PositionZ.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ")" +
                        " rLocal=" + localRadius.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                        " soiErr=" + soiError.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                }

                Debug.Log("[KMC-ORBIT-DIAG] END patch=" + item.Index);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[KMC-ORBIT-DIAG] FAILED patch=" + item.Index + " " +
                    ex.GetType().Name + " " + ex.Message);
            }
        }

        private static string FormatVector(Vector3d v)
        {
            return "(" +
                v.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," +
                v.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," +
                v.z.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ")";
        }

        private static double VectorAngleDegrees(Vector3d a, Vector3d b)
        {
            double ma = a.magnitude;
            double mb = b.magnitude;
            if (ma <= 1e-9 || mb <= 1e-9) return double.NaN;
            double c = Vector3d.Dot(a, b) / (ma * mb);
            if (c > 1.0) c = 1.0;
            if (c < -1.0) c = -1.0;
            return Math.Acos(c) * 180.0 / Math.PI;
        }

        private static bool IsSerializablePatchOrbit(OrbitMapOrbit orbit)
        {
            if (orbit == null) return false;
            return IsFinite(orbit.SemiMajorAxisMeters) &&
                   IsFinite(orbit.Eccentricity) &&
                   orbit.Eccentricity >= 0.0 &&
                   IsFinite(orbit.InclinationDegrees) &&
                   IsFinite(orbit.LongitudeOfAscendingNodeDegrees) &&
                   IsFinite(orbit.ArgumentOfPeriapsisDegrees) &&
                   IsFinite(orbit.EpochUniversalTimeSeconds) &&
                   IsFinite(orbit.MeanAnomalyAtEpochRadians) &&
                   IsFinite(orbit.PeriapsisMeters) &&
                   IsFinite(orbit.PositionX) &&
                   IsFinite(orbit.PositionY) &&
                   IsFinite(orbit.PositionZ);
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
