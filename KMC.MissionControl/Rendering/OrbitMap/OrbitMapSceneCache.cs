using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using KMC.Shared;

namespace KMC.MissionControl.Rendering.OrbitMap
{
    public sealed class OrbitMapSceneSnapshot
    {
        public double BodyRadiusMeters { get; internal set; }
        public string BodyName { get; internal set; }
        public string VesselName { get; internal set; }
        public OrbitMapOrbit ActiveOrbit { get; internal set; }
        public OrbitMapTarget Target { get; internal set; }
        public OrbitMapVector3[] ActiveOrbitPoints { get; internal set; }
        public OrbitMapVector3[] TargetOrbitPoints { get; internal set; }
        public List<OrbitMapVector3[]> PatchPoints { get; internal set; }
        public List<OrbitMapManeuverNode> ManeuverNodes { get; internal set; }
        public List<OrbitMapPatch> Patches { get; internal set; }
        public OrbitMapVector3 VesselPosition { get; internal set; }
        public OrbitMapVector3 TargetPosition { get; internal set; }
        public OrbitMapVector3 ApoapsisPosition { get; internal set; }
        public OrbitMapVector3 PeriapsisPosition { get; internal set; }
        public double UniversalTimeSeconds { get; internal set; }
        public DateTime SourceTimestampUtc { get; internal set; }

        public OrbitMapSceneSnapshot()
        {
            BodyName = string.Empty;
            VesselName = string.Empty;
            ActiveOrbitPoints = new OrbitMapVector3[0];
            TargetOrbitPoints = new OrbitMapVector3[0];
            PatchPoints = new List<OrbitMapVector3[]>();
            ManeuverNodes = new List<OrbitMapManeuverNode>();
            Patches = new List<OrbitMapPatch>();
        }
    }

    public sealed class OrbitMapSceneCache
    {
        private string _trajectoryFingerprint = string.Empty;
        private OrbitMapSceneSnapshot _current;
        public long GeometryRebuildCount { get; private set; }
        public long SnapshotUpdateCount { get; private set; }
        public OrbitMapSceneSnapshot Current { get { return _current; } }

        public OrbitMapSceneSnapshot Update(OrbitMapPacket packet)
        {
            if (packet == null || packet.ActiveOrbit == null) return _current;
            SnapshotUpdateCount++;
            string fingerprint = TrajectoryFingerprint(packet);
            bool rebuild = _current == null || !string.Equals(_trajectoryFingerprint, fingerprint, StringComparison.Ordinal);
            OrbitMapSceneSnapshot scene = new OrbitMapSceneSnapshot();
            scene.BodyRadiusMeters = packet.ReferenceBodyRadiusMeters;
            scene.BodyName = packet.ReferenceBodyName ?? string.Empty;
            scene.VesselName = packet.VesselName ?? string.Empty;
            scene.ActiveOrbit = packet.ActiveOrbit;
            scene.Target = packet.Target;
            scene.UniversalTimeSeconds = packet.UniversalTimeSeconds;
            scene.SourceTimestampUtc = packet.TimestampUtc;
            scene.VesselPosition = new OrbitMapVector3(packet.ActiveOrbit.PositionX, packet.ActiveOrbit.PositionY, packet.ActiveOrbit.PositionZ);
            scene.ApoapsisPosition = OrbitMapConicSampler.ApoapsisPosition(packet.ActiveOrbit);
            scene.PeriapsisPosition = OrbitMapConicSampler.PeriapsisPosition(packet.ActiveOrbit);
            if (packet.Target != null && packet.Target.Present)
                scene.TargetPosition = new OrbitMapVector3(packet.Target.PositionX, packet.Target.PositionY, packet.Target.PositionZ);
            scene.ManeuverNodes.AddRange(packet.ManeuverNodes);
            scene.Patches.AddRange(packet.Patches);

            if (rebuild)
            {
                scene.ActiveOrbitPoints = packet.ActiveOrbit.Eccentricity < 1.0 ? OrbitMapConicSampler.SampleClosedOrbit(packet.ActiveOrbit, 192) : new OrbitMapVector3[0];
                if (packet.Target != null && packet.Target.Present && packet.Target.Orbit != null && packet.Target.Orbit.Eccentricity < 1.0 && string.Equals(packet.Target.Orbit.ReferenceBodyName, packet.ReferenceBodyName, StringComparison.OrdinalIgnoreCase))
                    scene.TargetOrbitPoints = OrbitMapConicSampler.SampleClosedOrbit(packet.Target.Orbit, 192);
                for (int i = 0; i < packet.Patches.Count; i++)
                {
                    OrbitMapPatch patch = packet.Patches[i];
                    if (patch.Orbit != null && string.Equals(patch.Orbit.ReferenceBodyName, packet.ReferenceBodyName, StringComparison.OrdinalIgnoreCase))
                        scene.PatchPoints.Add(OrbitMapConicSampler.SamplePatch(patch, Math.Min(256, 192)));
                }
                GeometryRebuildCount++;
                _trajectoryFingerprint = fingerprint;
            }
            else if (_current != null)
            {
                scene.ActiveOrbitPoints = _current.ActiveOrbitPoints;
                scene.TargetOrbitPoints = _current.TargetOrbitPoints;
                scene.PatchPoints = _current.PatchPoints;
            }

            _current = scene;
            return scene;
        }

        private static string TrajectoryFingerprint(OrbitMapPacket packet)
        {
            StringBuilder b = new StringBuilder();
            b.Append(packet.ReferenceBodyName).Append('|').Append(R(packet.ReferenceBodyRadiusMeters));
            AppendOrbit(b, packet.ActiveOrbit);
            if (packet.Target != null)
            {
                b.Append('|').Append(packet.Target.Present).Append('|').Append(packet.Target.TargetType).Append('|').Append(packet.Target.TargetId);
                AppendOrbit(b, packet.Target.Orbit);
            }
            for (int i = 0; i < packet.ManeuverNodes.Count; i++)
            {
                OrbitMapManeuverNode n = packet.ManeuverNodes[i];
                b.Append("|N|").Append(R(n.UniversalTimeSeconds)).Append('|').Append(R(n.ProgradeDeltaVMetersPerSecond)).Append('|').Append(R(n.NormalDeltaVMetersPerSecond)).Append('|').Append(R(n.RadialDeltaVMetersPerSecond));
            }
            for (int i = 0; i < packet.Patches.Count; i++)
            {
                OrbitMapPatch p = packet.Patches[i];
                b.Append("|P|").Append(p.Index).Append('|').Append(p.TransitionType).Append('|').Append(p.NextBodyName).Append('|').Append(R(p.StartUniversalTimeSeconds)).Append('|').Append(R(p.EndUniversalTimeSeconds));
                AppendOrbit(b, p.Orbit);
            }
            return b.ToString();
        }

        private static void AppendOrbit(StringBuilder b, OrbitMapOrbit o)
        {
            if (o == null) { b.Append("|O:null"); return; }
            b.Append("|O|").Append(o.ReferenceBodyName).Append('|').Append(R(o.SemiMajorAxisMeters)).Append('|').Append(R(o.Eccentricity)).Append('|').Append(R(o.InclinationDegrees)).Append('|').Append(R(o.LongitudeOfAscendingNodeDegrees)).Append('|').Append(R(o.ArgumentOfPeriapsisDegrees)).Append('|').Append(R(o.EpochUniversalTimeSeconds)).Append('|').Append(R(o.MeanAnomalyAtEpochRadians));
        }

        private static string R(double value) { return double.IsNaN(value) ? "NaN" : value.ToString("R", CultureInfo.InvariantCulture); }
    }
}
