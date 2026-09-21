using System;
using System.Collections.Generic;
using System.Globalization;

namespace KMC.Shared
{
    public sealed class OrbitMapPacket
    {
        public const string ProtocolId = "KMC-ORBITMAP1";
        public const int TelemetryPort = 5110;
        public const int MaxManeuverNodes = 8;
        public const int MaxPatches = 12;
        public const int MaxPatchSamples = 64;
        public const int MaxTotalPatchSamples = 128;
        public const int MaxBodies = 16;

        public long Sequence { get; set; }
        public DateTime TimestampUtc { get; set; }
        public string VesselId { get; set; }
        public string VesselName { get; set; }
        public double UniversalTimeSeconds { get; set; }
        public string ReferenceBodyName { get; set; }
        public double ReferenceBodyRadiusMeters { get; set; }
        public double ReferenceBodySoiRadiusMeters { get; set; }
        public OrbitMapOrbit ActiveOrbit { get; set; }
        public OrbitMapTarget Target { get; set; }
        public List<OrbitMapManeuverNode> ManeuverNodes { get; private set; }
        public List<OrbitMapPatch> Patches { get; private set; }
        public List<OrbitMapBody> Bodies { get; private set; }

        public OrbitMapPacket()
        {
            TimestampUtc = DateTime.UtcNow;
            VesselId = string.Empty;
            VesselName = string.Empty;
            ReferenceBodyName = string.Empty;
            ManeuverNodes = new List<OrbitMapManeuverNode>();
            Patches = new List<OrbitMapPatch>();
            Bodies = new List<OrbitMapBody>();
        }

        public string Serialize()
        {
            if (ManeuverNodes.Count > MaxManeuverNodes || Patches.Count > MaxPatches || Bodies.Count > MaxBodies)
                throw new InvalidOperationException("Orbit map collection exceeds protocol bounds.");
            int totalPatchSamples = 0;
            for (int i = 0; i < Patches.Count; i++) totalPatchSamples += Patches[i] != null ? Patches[i].Samples.Count : 0;
            if (totalPatchSamples > MaxTotalPatchSamples) throw new InvalidOperationException("Orbit map total patch sample collection exceeds protocol bounds.");

            List<string> fields = new List<string>();
            fields.Add(ProtocolId);
            fields.Add(Sequence.ToString(CultureInfo.InvariantCulture));
            fields.Add(TimestampUtc.Ticks.ToString(CultureInfo.InvariantCulture));
            fields.Add(Escape(VesselId));
            fields.Add(Escape(VesselName));
            fields.Add(F(UniversalTimeSeconds));
            fields.Add(Escape(ReferenceBodyName));
            fields.Add(F(ReferenceBodyRadiusMeters));
            fields.Add(F(ReferenceBodySoiRadiusMeters));
            fields.Add(SerializeOrbit(ActiveOrbit));
            fields.Add(SerializeTarget(Target));
            fields.Add(Bodies.Count.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < Bodies.Count; i++) fields.Add(SerializeBody(Bodies[i]));
            fields.Add(ManeuverNodes.Count.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < ManeuverNodes.Count; i++) fields.Add(SerializeNode(ManeuverNodes[i]));
            fields.Add(Patches.Count.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < Patches.Count; i++) fields.Add(SerializePatch(Patches[i]));
            return string.Join("|", fields.ToArray());
        }

        public static bool TryParse(string message, out OrbitMapPacket packet)
        {
            packet = null;
            if (string.IsNullOrWhiteSpace(message)) return false;
            try
            {
                string[] fields = message.Split('|');
                int p = 0;
                if (fields.Length < 14 || !string.Equals(fields[p++], ProtocolId, StringComparison.Ordinal)) return false;

                long sequence;
                long ticks;
                double valueDouble;
                if (!long.TryParse(fields[p++], NumberStyles.Integer, CultureInfo.InvariantCulture, out sequence)) return false;
                if (!long.TryParse(fields[p++], NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks)) return false;

                DateTime timestamp;
                try { timestamp = new DateTime(ticks, DateTimeKind.Utc); }
                catch (ArgumentOutOfRangeException) { return false; }

                OrbitMapPacket value = new OrbitMapPacket();
                value.Sequence = sequence;
                value.TimestampUtc = timestamp;
                value.VesselId = Unescape(fields[p++]);
                value.VesselName = Unescape(fields[p++]);
                if (!TryFinite(fields[p++], out valueDouble)) return false;
                value.UniversalTimeSeconds = valueDouble;
                value.ReferenceBodyName = Unescape(fields[p++]);
                if (!TryFinite(fields[p++], out valueDouble) || valueDouble <= 0.0) return false;
                value.ReferenceBodyRadiusMeters = valueDouble;
                if (!TryFinite(fields[p++], out valueDouble) || valueDouble < 0.0) return false;
                value.ReferenceBodySoiRadiusMeters = valueDouble;

                OrbitMapOrbit activeOrbit;
                if (!TryParseOrbit(fields[p++], out activeOrbit) || activeOrbit == null) return false;
                value.ActiveOrbit = activeOrbit;

                OrbitMapTarget target;
                if (!TryParseTarget(fields[p++], out target)) return false;
                value.Target = target;

                int bodyCount;
                if (p >= fields.Length || !int.TryParse(fields[p++], NumberStyles.Integer, CultureInfo.InvariantCulture, out bodyCount) || bodyCount < 0 || bodyCount > MaxBodies) return false;
                HashSet<string> bodyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < bodyCount; i++)
                {
                    OrbitMapBody body;
                    if (p >= fields.Length || !TryParseBody(fields[p++], out body) || body == null || string.IsNullOrWhiteSpace(body.Name)) return false;
                    if (!bodyNames.Add(body.Name)) return false;
                    value.Bodies.Add(body);
                }

                int nodeCount;
                if (p >= fields.Length || !int.TryParse(fields[p++], NumberStyles.Integer, CultureInfo.InvariantCulture, out nodeCount) || nodeCount < 0 || nodeCount > MaxManeuverNodes) return false;
                for (int i = 0; i < nodeCount; i++)
                {
                    OrbitMapManeuverNode node;
                    if (p >= fields.Length || !TryParseNode(fields[p++], out node)) return false;
                    value.ManeuverNodes.Add(node);
                }

                int patchCount;
                if (p >= fields.Length || !int.TryParse(fields[p++], NumberStyles.Integer, CultureInfo.InvariantCulture, out patchCount) || patchCount < 0 || patchCount > MaxPatches) return false;
                int previousIndex = -1;
                int totalPatchSamples = 0;
                for (int i = 0; i < patchCount; i++)
                {
                    OrbitMapPatch patch;
                    if (p >= fields.Length || !TryParsePatch(fields[p++], out patch) || patch.Index <= previousIndex) return false;
                    previousIndex = patch.Index;
                    totalPatchSamples += patch.Samples.Count;
                    if (totalPatchSamples > MaxTotalPatchSamples) return false;
                    value.Patches.Add(patch);
                }
                if (p != fields.Length) return false;
                packet = value;
                return true;
            }
            catch (Exception)
            {
                packet = null;
                return false;
            }
        }

        private static string SerializeOrbit(OrbitMapOrbit orbit)
        {
            if (orbit == null) return string.Empty;
            return string.Join("~", new[] { Escape(orbit.ReferenceBodyName), F(orbit.SemiMajorAxisMeters), F(orbit.Eccentricity), F(orbit.InclinationDegrees), F(orbit.LongitudeOfAscendingNodeDegrees), F(orbit.ArgumentOfPeriapsisDegrees), F(orbit.EpochUniversalTimeSeconds), F(orbit.MeanAnomalyAtEpochRadians), FO(orbit.ApoapsisMeters), F(orbit.PeriapsisMeters), FO(orbit.PeriodSeconds), F(orbit.PositionX), F(orbit.PositionY), F(orbit.PositionZ) });
        }

        private static bool TryParseOrbit(string text, out OrbitMapOrbit orbit)
        {
            orbit = null;
            if (string.IsNullOrEmpty(text)) return true;
            string[] f = text.Split('~');
            if (f.Length != 14) return false;
            double d;
            OrbitMapOrbit o = new OrbitMapOrbit();
            o.ReferenceBodyName = Unescape(f[0]);
            if (!TryFinite(f[1], out d)) return false; o.SemiMajorAxisMeters = d;
            if (!TryFinite(f[2], out d) || d < 0.0) return false; o.Eccentricity = d;
            if (!TryFinite(f[3], out d)) return false; o.InclinationDegrees = d;
            if (!TryFinite(f[4], out d)) return false; o.LongitudeOfAscendingNodeDegrees = d;
            if (!TryFinite(f[5], out d)) return false; o.ArgumentOfPeriapsisDegrees = d;
            if (!TryFinite(f[6], out d)) return false; o.EpochUniversalTimeSeconds = d;
            if (!TryFinite(f[7], out d)) return false; o.MeanAnomalyAtEpochRadians = d;
            if (!TryOptionalFinite(f[8], out d)) return false; o.ApoapsisMeters = d;
            if (!TryFinite(f[9], out d)) return false; o.PeriapsisMeters = d;
            if (!TryOptionalFinite(f[10], out d)) return false; o.PeriodSeconds = d;
            if (!TryFinite(f[11], out d)) return false; o.PositionX = d;
            if (!TryFinite(f[12], out d)) return false; o.PositionY = d;
            if (!TryFinite(f[13], out d)) return false; o.PositionZ = d;
            orbit = o;
            return true;
        }

        private static string SerializeTarget(OrbitMapTarget target)
        {
            if (target == null || !target.Present) return "0";
            return string.Join("~", new[] { "1", Escape(target.TargetType), Escape(target.TargetId), Escape(target.TargetName), SerializeOrbit(target.Orbit), F(target.PositionX), F(target.PositionY), F(target.PositionZ) });
        }

        private static bool TryParseTarget(string text, out OrbitMapTarget target)
        {
            target = new OrbitMapTarget();
            if (text == "0" || string.IsNullOrEmpty(text)) return true;
            string[] f = text.Split('~');
            if (f.Length != 21 || f[0] != "1") return false;
            target.Present = true;
            target.TargetType = Unescape(f[1]);
            target.TargetId = Unescape(f[2]);
            target.TargetName = Unescape(f[3]);
            OrbitMapOrbit orbit;
            if (!TryParseOrbit(string.Join("~", f, 4, 14), out orbit)) return false;
            target.Orbit = orbit;
            double d;
            if (!TryFinite(f[18], out d)) return false; target.PositionX = d;
            if (!TryFinite(f[19], out d)) return false; target.PositionY = d;
            if (!TryFinite(f[20], out d)) return false; target.PositionZ = d;
            return true;
        }

        private static string SerializeBody(OrbitMapBody body)
        {
            if (body == null) throw new InvalidOperationException("Null orbit map body.");
            List<string> f = new List<string>();
            f.Add(Escape(body.Name));
            f.Add(Escape(body.ParentName));
            f.Add(F(body.RadiusMeters));
            f.Add(F(body.SoiRadiusMeters));
            f.Add(F(body.GravParameter));
            if (body.Orbit == null)
            {
                f.Add("0");
                for (int i = 0; i < 14; i++) f.Add(string.Empty);
            }
            else
            {
                f.Add("1");
                string[] orbitFields = SerializeOrbit(body.Orbit).Split('~');
                for (int i = 0; i < orbitFields.Length; i++) f.Add(orbitFields[i]);
            }
            f.Add(F(body.PositionX));
            f.Add(F(body.PositionY));
            f.Add(F(body.PositionZ));
            return string.Join("~", f.ToArray());
        }

        private static bool TryParseBody(string text, out OrbitMapBody body)
        {
            body = null;
            string[] f = text.Split('~');
            if (f.Length != 23) return false;
            double d;
            OrbitMapBody value = new OrbitMapBody();
            value.Name = Unescape(f[0]);
            value.ParentName = Unescape(f[1]);
            if (!TryFinite(f[2], out d) || d <= 0.0) return false; value.RadiusMeters = d;
            if (!TryFinite(f[3], out d) || d < 0.0) return false; value.SoiRadiusMeters = d;
            if (!TryFinite(f[4], out d) || d <= 0.0) return false; value.GravParameter = d;
            if (f[5] == "1")
            {
                OrbitMapOrbit orbit;
                if (!TryParseOrbit(string.Join("~", f, 6, 14), out orbit) || orbit == null) return false;
                value.Orbit = orbit;
            }
            else if (f[5] != "0") return false;
            if (!TryFinite(f[20], out d)) return false; value.PositionX = d;
            if (!TryFinite(f[21], out d)) return false; value.PositionY = d;
            if (!TryFinite(f[22], out d)) return false; value.PositionZ = d;
            body = value;
            return true;
        }

        private static string SerializeNode(OrbitMapManeuverNode n)
        {
            return string.Join("~", new[] { n.Index.ToString(CultureInfo.InvariantCulture), F(n.UniversalTimeSeconds), F(n.ProgradeDeltaVMetersPerSecond), F(n.NormalDeltaVMetersPerSecond), F(n.RadialDeltaVMetersPerSecond), F(n.TotalDeltaVMetersPerSecond), F(n.PositionX), F(n.PositionY), F(n.PositionZ), n.PatchIndex.ToString(CultureInfo.InvariantCulture) });
        }

        private static bool TryParseNode(string text, out OrbitMapManeuverNode node)
        {
            node = null;
            string[] f = text.Split('~');
            int index;
            int patchIndex;
            if (f.Length != 10 || !int.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out index) || !int.TryParse(f[9], NumberStyles.Integer, CultureInfo.InvariantCulture, out patchIndex)) return false;
            double d;
            OrbitMapManeuverNode v = new OrbitMapManeuverNode();
            v.Index = index;
            v.PatchIndex = patchIndex;
            if (!TryFinite(f[1], out d)) return false; v.UniversalTimeSeconds = d;
            if (!TryFinite(f[2], out d)) return false; v.ProgradeDeltaVMetersPerSecond = d;
            if (!TryFinite(f[3], out d)) return false; v.NormalDeltaVMetersPerSecond = d;
            if (!TryFinite(f[4], out d)) return false; v.RadialDeltaVMetersPerSecond = d;
            if (!TryFinite(f[5], out d)) return false; v.TotalDeltaVMetersPerSecond = d;
            if (!TryFinite(f[6], out d)) return false; v.PositionX = d;
            if (!TryFinite(f[7], out d)) return false; v.PositionY = d;
            if (!TryFinite(f[8], out d)) return false; v.PositionZ = d;
            node = v;
            return true;
        }

        private static string SerializePatch(OrbitMapPatch patch)
        {
            if (patch == null) throw new InvalidOperationException("Orbit map patch is required.");
            if (patch.Samples.Count > MaxPatchSamples) throw new InvalidOperationException("Orbit map patch sample collection exceeds protocol bounds.");
            List<string> fields = new List<string>();
            fields.Add(patch.Index.ToString(CultureInfo.InvariantCulture));
            fields.Add(F(patch.StartUniversalTimeSeconds));
            fields.Add(FO(patch.EndUniversalTimeSeconds));
            fields.Add(Escape(patch.TransitionType));
            fields.Add(Escape(patch.NextBodyName));
            string orbit = SerializeOrbit(patch.Orbit);
            string[] orbitFields = orbit.Split('~');
            for (int i = 0; i < orbitFields.Length; i++) fields.Add(orbitFields[i]);
            fields.Add(patch.Samples.Count.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < patch.Samples.Count; i++) fields.Add(SerializePatchSample(patch.Samples[i]));
            return string.Join("~", fields.ToArray());
        }

        private static string SerializePatchSample(OrbitMapPatchSample sample)
        {
            if (sample == null) throw new InvalidOperationException("Orbit map patch sample is required.");
            return string.Join(",", new[] { F(sample.UniversalTimeSeconds), F(sample.PositionX), F(sample.PositionY), F(sample.PositionZ), F(sample.ReferenceBodyPositionX), F(sample.ReferenceBodyPositionY), F(sample.ReferenceBodyPositionZ) });
        }

        private static bool TryParsePatch(string text, out OrbitMapPatch patch)
        {
            patch = null;
            string[] f = text.Split('~');
            int index;
            if (f.Length < 19 || !int.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out index)) return false;
            double d;
            OrbitMapPatch v = new OrbitMapPatch();
            v.Index = index;
            if (!TryFinite(f[1], out d)) return false; v.StartUniversalTimeSeconds = d;
            if (!TryOptionalFinite(f[2], out d)) return false; v.EndUniversalTimeSeconds = d;
            v.TransitionType = Unescape(f[3]);
            v.NextBodyName = Unescape(f[4]);
            OrbitMapOrbit orbit;
            if (!TryParseOrbit(string.Join("~", f, 5, 14), out orbit) || orbit == null) return false;
            v.Orbit = orbit;

            // Backward compatibility: 14.22.8-14.22.12 packets ended after the orbit.
            if (f.Length == 19)
            {
                patch = v;
                return true;
            }

            int sampleCount;
            if (!int.TryParse(f[19], NumberStyles.Integer, CultureInfo.InvariantCulture, out sampleCount) ||
                sampleCount < 0 || sampleCount > MaxPatchSamples || f.Length != 20 + sampleCount) return false;
            for (int i = 0; i < sampleCount; i++)
            {
                OrbitMapPatchSample sample;
                if (!TryParsePatchSample(f[20 + i], out sample)) return false;
                v.Samples.Add(sample);
            }
            patch = v;
            return true;
        }

        private static bool TryParsePatchSample(string text, out OrbitMapPatchSample sample)
        {
            sample = null;
            string[] f = text.Split(',');
            if (f.Length != 7) return false;
            double d;
            OrbitMapPatchSample v = new OrbitMapPatchSample();
            if (!TryFinite(f[0], out d)) return false; v.UniversalTimeSeconds = d;
            if (!TryFinite(f[1], out d)) return false; v.PositionX = d;
            if (!TryFinite(f[2], out d)) return false; v.PositionY = d;
            if (!TryFinite(f[3], out d)) return false; v.PositionZ = d;
            if (!TryFinite(f[4], out d)) return false; v.ReferenceBodyPositionX = d;
            if (!TryFinite(f[5], out d)) return false; v.ReferenceBodyPositionY = d;
            if (!TryFinite(f[6], out d)) return false; v.ReferenceBodyPositionZ = d;
            sample = v;
            return true;
        }

        private static string Escape(string value) { return Uri.EscapeDataString(value ?? string.Empty).Replace("~", "%7E"); }
        private static string Unescape(string value) { return Uri.UnescapeDataString(value ?? string.Empty); }
        private static string F(double value) { if (!IsFinite(value)) throw new InvalidOperationException("Non-finite required orbit map value."); return value.ToString("R", CultureInfo.InvariantCulture); }
        private static string FO(double value) { return IsFinite(value) ? value.ToString("R", CultureInfo.InvariantCulture) : string.Empty; }
        private static bool TryFinite(string text, out double value) { return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && IsFinite(value); }
        private static bool TryOptionalFinite(string text, out double value) { if (string.IsNullOrEmpty(text)) { value = double.NaN; return true; } return TryFinite(text, out value); }
        private static bool IsFinite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
    }

    public sealed class OrbitMapOrbit
    {
        public string ReferenceBodyName { get; set; }
        public double SemiMajorAxisMeters { get; set; }
        public double Eccentricity { get; set; }
        public double InclinationDegrees { get; set; }
        public double LongitudeOfAscendingNodeDegrees { get; set; }
        public double ArgumentOfPeriapsisDegrees { get; set; }
        public double EpochUniversalTimeSeconds { get; set; }
        public double MeanAnomalyAtEpochRadians { get; set; }
        public double ApoapsisMeters { get; set; }
        public double PeriapsisMeters { get; set; }
        public double PeriodSeconds { get; set; }
        public double PositionX { get; set; }
        public double PositionY { get; set; }
        public double PositionZ { get; set; }
    }

    public sealed class OrbitMapTarget
    {
        public bool Present { get; set; }
        public string TargetType { get; set; }
        public string TargetId { get; set; }
        public string TargetName { get; set; }
        public OrbitMapOrbit Orbit { get; set; }
        public double PositionX { get; set; }
        public double PositionY { get; set; }
        public double PositionZ { get; set; }
        public OrbitMapTarget() { TargetType = string.Empty; TargetId = string.Empty; TargetName = string.Empty; }
    }

    public sealed class OrbitMapBody
    {
        public string Name { get; set; }
        public string ParentName { get; set; }
        public double RadiusMeters { get; set; }
        public double SoiRadiusMeters { get; set; }
        public double GravParameter { get; set; }
        public OrbitMapOrbit Orbit { get; set; }
        public double PositionX { get; set; }
        public double PositionY { get; set; }
        public double PositionZ { get; set; }
        public OrbitMapBody() { Name = string.Empty; ParentName = string.Empty; }
    }

    public sealed class OrbitMapManeuverNode
    {
        public int Index { get; set; }
        public double UniversalTimeSeconds { get; set; }
        public double ProgradeDeltaVMetersPerSecond { get; set; }
        public double NormalDeltaVMetersPerSecond { get; set; }
        public double RadialDeltaVMetersPerSecond { get; set; }
        public double TotalDeltaVMetersPerSecond { get; set; }
        public double PositionX { get; set; }
        public double PositionY { get; set; }
        public double PositionZ { get; set; }
        public int PatchIndex { get; set; }
    }

    public sealed class OrbitMapPatch
    {
        public int Index { get; set; }
        public double StartUniversalTimeSeconds { get; set; }
        public double EndUniversalTimeSeconds { get; set; }
        public string TransitionType { get; set; }
        public string NextBodyName { get; set; }
        public OrbitMapOrbit Orbit { get; set; }
        public List<OrbitMapPatchSample> Samples { get; private set; }
        public OrbitMapPatch() { TransitionType = string.Empty; NextBodyName = string.Empty; Samples = new List<OrbitMapPatchSample>(); }
    }

    public sealed class OrbitMapPatchSample
    {
        public double UniversalTimeSeconds { get; set; }
        public double PositionX { get; set; }
        public double PositionY { get; set; }
        public double PositionZ { get; set; }
        public double ReferenceBodyPositionX { get; set; }
        public double ReferenceBodyPositionY { get; set; }
        public double ReferenceBodyPositionZ { get; set; }
    }
}
