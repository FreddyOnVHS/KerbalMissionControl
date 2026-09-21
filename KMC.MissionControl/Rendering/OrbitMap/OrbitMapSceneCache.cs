using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using KMC.Shared;

namespace KMC.MissionControl.Rendering.OrbitMap
{
    public enum OrbitMapPatchRenderKind
    {
        PrimaryInitial,
        Encounter,
        PrimaryLater
    }

    public sealed class OrbitMapSceneBody
    {
        public OrbitMapBody Body { get; internal set; }
        public OrbitMapVector3 Position { get; internal set; }
        public OrbitMapVector3[] OrbitPoints { get; internal set; }
        public OrbitMapSceneBody() { OrbitPoints = new OrbitMapVector3[0]; }
    }

    public sealed class OrbitMapSceneEncounterBody
    {
        public OrbitMapBody Body { get; internal set; }
        public OrbitMapVector3 Position { get; internal set; }
        public int PatchIndex { get; internal set; }
        public double EncounterUniversalTimeSeconds { get; internal set; }
    }

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
        public List<OrbitMapPatchRenderKind> PatchRenderKinds { get; internal set; }
        public List<OrbitMapManeuverNode> ManeuverNodes { get; internal set; }
        public List<OrbitMapPatch> Patches { get; internal set; }
        public List<OrbitMapSceneBody> ChildBodies { get; internal set; }
        public List<OrbitMapSceneEncounterBody> EncounterBodies { get; internal set; }
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
            PatchRenderKinds = new List<OrbitMapPatchRenderKind>();
            ManeuverNodes = new List<OrbitMapManeuverNode>();
            Patches = new List<OrbitMapPatch>();
            ChildBodies = new List<OrbitMapSceneBody>();
            EncounterBodies = new List<OrbitMapSceneEncounterBody>();
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
                for (int i = 0; i < packet.Bodies.Count; i++)
                {
                    OrbitMapBody body = packet.Bodies[i];
                    if (body == null || string.Equals(body.Name, packet.ReferenceBodyName, StringComparison.OrdinalIgnoreCase) || body.Orbit == null) continue;
                    if (!string.Equals(body.ParentName, packet.ReferenceBodyName, StringComparison.OrdinalIgnoreCase)) continue;
                    OrbitMapSceneBody sceneBody = new OrbitMapSceneBody();
                    sceneBody.Body = body;
                    sceneBody.Position = new OrbitMapVector3(body.PositionX, body.PositionY, body.PositionZ);
                    if (body.Orbit.Eccentricity < 1.0) sceneBody.OrbitPoints = OrbitMapConicSampler.SampleClosedOrbit(body.Orbit, 160);
                    scene.ChildBodies.Add(sceneBody);
                }

                OrbitMapBody primaryBody = FindBody(packet.Bodies, packet.ReferenceBodyName);
                int primaryPassCount = 0;
                for (int i = 0; i < packet.Patches.Count; i++)
                {
                    OrbitMapPatch patch = packet.Patches[i];
                    if (patch.Orbit == null) continue;
                    if (string.Equals(patch.Orbit.ReferenceBodyName, packet.ReferenceBodyName, StringComparison.OrdinalIgnoreCase))
                    {
                        OrbitMapVector3[] sampled = patch.Samples.Count > 1
                            ? OrbitMapSystemTransform.AuthoritativePatchPoints(patch)
                            : OrbitMapConicSampler.SamplePatch(patch, Math.Min(256, 192), primaryBody != null ? primaryBody.GravParameter : 0.0);
                        if (sampled.Length > 0)
                        {
                            scene.PatchPoints.Add(sampled);
                            scene.PatchRenderKinds.Add(
                                primaryPassCount == 0 ? OrbitMapPatchRenderKind.PrimaryInitial : OrbitMapPatchRenderKind.PrimaryLater);
                            primaryPassCount++;
                        }
                        continue;
                    }

                    OrbitMapBody child = FindBody(packet.Bodies, patch.Orbit.ReferenceBodyName);
                    if (child != null && string.Equals(child.ParentName, packet.ReferenceBodyName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (patch.Samples.Count > 1)
                        {
                            OrbitMapVector3[] authoritative = OrbitMapSystemTransform.AuthoritativePatchPoints(patch);
                            scene.PatchPoints.Add(authoritative);
                            scene.PatchRenderKinds.Add(OrbitMapPatchRenderKind.Encounter);

                            OrbitMapPatchSample closest = FindClosestApproachSample(patch);
                            OrbitMapSceneEncounterBody encounterBody = new OrbitMapSceneEncounterBody();
                            encounterBody.Body = child;
                            encounterBody.Position = new OrbitMapVector3(
                                closest.ReferenceBodyPositionX,
                                closest.ReferenceBodyPositionY,
                                closest.ReferenceBodyPositionZ);
                            encounterBody.PatchIndex = patch.Index;
                            encounterBody.EncounterUniversalTimeSeconds = closest.UniversalTimeSeconds;
                            scene.EncounterBodies.Add(encounterBody);
                        }
                    }
                }

                GeometryRebuildCount++;
                _trajectoryFingerprint = fingerprint;
            }
            else if (_current != null)
            {
                scene.ActiveOrbitPoints = _current.ActiveOrbitPoints;
                scene.TargetOrbitPoints = _current.TargetOrbitPoints;
                scene.PatchPoints = _current.PatchPoints;
                scene.PatchRenderKinds = _current.PatchRenderKinds;
                scene.ChildBodies = _current.ChildBodies;
                scene.EncounterBodies = _current.EncounterBodies;
                for (int i = 0; i < scene.ChildBodies.Count; i++)
                {
                    OrbitMapSceneBody body = scene.ChildBodies[i];
                    if (body != null && body.Body != null) body.Position = new OrbitMapVector3(body.Body.PositionX, body.Body.PositionY, body.Body.PositionZ);
                }
            }

            _current = scene;
            return scene;
        }

        private static OrbitMapPatchSample FindClosestApproachSample(OrbitMapPatch patch)
        {
            OrbitMapPatchSample best = patch.Samples[0];
            double bestDistanceSquared = DistanceToReferenceBodySquared(best);

            for (int i = 1; i < patch.Samples.Count; i++)
            {
                OrbitMapPatchSample candidate = patch.Samples[i];
                double candidateDistanceSquared = DistanceToReferenceBodySquared(candidate);
                if (candidateDistanceSquared < bestDistanceSquared)
                {
                    best = candidate;
                    bestDistanceSquared = candidateDistanceSquared;
                }
            }

            return best;
        }

        private static double DistanceToReferenceBodySquared(OrbitMapPatchSample sample)
        {
            double dx = sample.PositionX - sample.ReferenceBodyPositionX;
            double dy = sample.PositionY - sample.ReferenceBodyPositionY;
            double dz = sample.PositionZ - sample.ReferenceBodyPositionZ;
            return dx * dx + dy * dy + dz * dz;
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
            for (int i = 0; i < packet.Bodies.Count; i++)
            {
                OrbitMapBody body = packet.Bodies[i];
                if (body == null) continue;
                b.Append("|B|").Append(body.Name).Append('|').Append(body.ParentName).Append('|').Append(R(body.RadiusMeters)).Append('|').Append(R(body.SoiRadiusMeters));
                AppendOrbit(b, body.Orbit);
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
                b.Append("|S|").Append(p.Samples.Count);
                if (p.Samples.Count > 0)
                {
                    OrbitMapPatchSample first = p.Samples[0];
                    OrbitMapPatchSample last = p.Samples[p.Samples.Count - 1];
                    b.Append('|').Append(R(first.UniversalTimeSeconds)).Append('|').Append(R(first.PositionX)).Append('|').Append(R(first.PositionY)).Append('|').Append(R(first.PositionZ));
                    b.Append('|').Append(R(last.UniversalTimeSeconds)).Append('|').Append(R(last.PositionX)).Append('|').Append(R(last.PositionY)).Append('|').Append(R(last.PositionZ));
                }
            }
            return b.ToString();
        }

        private static OrbitMapBody FindBody(List<OrbitMapBody> bodies, string name)
        {
            if (bodies == null || string.IsNullOrWhiteSpace(name)) return null;
            for (int i = 0; i < bodies.Count; i++)
            {
                OrbitMapBody body = bodies[i];
                if (body != null && string.Equals(body.Name, name, StringComparison.OrdinalIgnoreCase)) return body;
            }
            return null;
        }

        private static void AppendOrbit(StringBuilder b, OrbitMapOrbit o)
        {
            if (o == null) { b.Append("|O:null"); return; }
            b.Append("|O|").Append(o.ReferenceBodyName).Append('|').Append(R(o.SemiMajorAxisMeters)).Append('|').Append(R(o.Eccentricity)).Append('|').Append(R(o.InclinationDegrees)).Append('|').Append(R(o.LongitudeOfAscendingNodeDegrees)).Append('|').Append(R(o.ArgumentOfPeriapsisDegrees)).Append('|').Append(R(o.EpochUniversalTimeSeconds)).Append('|').Append(R(o.MeanAnomalyAtEpochRadians));
        }

        private static string R(double value) { return double.IsNaN(value) ? "NaN" : value.ToString("R", CultureInfo.InvariantCulture); }
    }
}
