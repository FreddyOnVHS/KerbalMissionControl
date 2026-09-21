using System;
using KMC.Shared;

namespace KMC.MissionControl.Rendering.OrbitMap
{
    public static class OrbitMapSystemTransform
    {
        public static OrbitMapVector3[] AuthoritativePatchPoints(OrbitMapPatch patch)
        {
            if (patch == null || patch.Samples == null || patch.Samples.Count == 0) return new OrbitMapVector3[0];
            OrbitMapVector3[] result = new OrbitMapVector3[patch.Samples.Count];
            for (int i = 0; i < patch.Samples.Count; i++)
            {
                OrbitMapPatchSample sample = patch.Samples[i];
                result[i] = new OrbitMapVector3(sample.PositionX, sample.PositionY, sample.PositionZ);
            }
            return result;
        }

        public static OrbitMapVector3 AuthoritativeReferenceBodyPosition(OrbitMapPatch patch)
        {
            if (patch == null || patch.Samples == null || patch.Samples.Count == 0) return new OrbitMapVector3();
            OrbitMapPatchSample sample = patch.Samples[0];
            return new OrbitMapVector3(sample.ReferenceBodyPositionX, sample.ReferenceBodyPositionY, sample.ReferenceBodyPositionZ);
        }
        public static OrbitMapVector3 BodyPositionAtUniversalTime(OrbitMapBody body, double universalTimeSeconds)
        {
            if (body == null) return new OrbitMapVector3();
            if (body.Orbit == null) return new OrbitMapVector3(body.PositionX, body.PositionY, body.PositionZ);
            OrbitMapVector3 position = OrbitMapConicSampler.PositionAtUniversalTime(body.Orbit, universalTimeSeconds);
            if (position.Magnitude <= 0.0 || double.IsNaN(position.X) || double.IsInfinity(position.X))
                return new OrbitMapVector3(body.PositionX, body.PositionY, body.PositionZ);
            return position;
        }

        public static OrbitMapVector3 BodyPositionAtUniversalTime(OrbitMapBody body, double stateUniversalTimeSeconds, double targetUniversalTimeSeconds)
        {
            if (body == null) return new OrbitMapVector3();
            OrbitMapVector3 current = new OrbitMapVector3(body.PositionX, body.PositionY, body.PositionZ);
            if (body.Orbit == null || Math.Abs(targetUniversalTimeSeconds - stateUniversalTimeSeconds) < 1e-9) return current;
            if (body.Orbit.Eccentricity >= 1.0 || double.IsNaN(body.Orbit.PeriodSeconds) || double.IsInfinity(body.Orbit.PeriodSeconds) || body.Orbit.PeriodSeconds <= 0.0) return current;

            double currentTrueAnomaly = TrueAnomalyFromCanonicalPosition(body.Orbit, current);
            if (double.IsNaN(currentTrueAnomaly) || double.IsInfinity(currentTrueAnomaly)) return current;

            double e = body.Orbit.Eccentricity;
            double sinHalf = Math.Sqrt(Math.Max(0.0, 1.0 - e)) * Math.Sin(currentTrueAnomaly * 0.5);
            double cosHalf = Math.Sqrt(1.0 + e) * Math.Cos(currentTrueAnomaly * 0.5);
            double eccentricAnomaly = 2.0 * Math.Atan2(sinHalf, cosHalf);
            double meanAnomalyAtState = eccentricAnomaly - e * Math.Sin(eccentricAnomaly);
            double meanMotion = 2.0 * Math.PI / body.Orbit.PeriodSeconds;
            double meanAnomaly = NormalizeRadians(meanAnomalyAtState + meanMotion * (targetUniversalTimeSeconds - stateUniversalTimeSeconds));
            double targetEccentricAnomaly = SolveEccentricAnomaly(meanAnomaly, e);
            double targetSinHalf = Math.Sqrt(1.0 + e) * Math.Sin(targetEccentricAnomaly * 0.5);
            double targetCosHalf = Math.Sqrt(Math.Max(0.0, 1.0 - e)) * Math.Cos(targetEccentricAnomaly * 0.5);
            double targetTrueAnomaly = NormalizeRadians(2.0 * Math.Atan2(targetSinHalf, targetCosHalf));
            return OrbitMapConicSampler.PositionAtTrueAnomaly(body.Orbit, targetTrueAnomaly);
        }

        public static OrbitMapVector3[] Translate(OrbitMapVector3[] points, OrbitMapVector3 offset)
        {
            if (points == null) return new OrbitMapVector3[0];
            OrbitMapVector3[] result = new OrbitMapVector3[points.Length];
            for (int i = 0; i < points.Length; i++)
                result[i] = new OrbitMapVector3(points[i].X + offset.X, points[i].Y + offset.Y, points[i].Z + offset.Z);
            return result;
        }

        public static OrbitMapVector3[] SamplePatchInPrimaryFrame(OrbitMapPatch patch, OrbitMapBody referenceBody, int sampleCount)
        {
            return SamplePatchInPrimaryFrame(patch, referenceBody, patch != null ? patch.StartUniversalTimeSeconds : 0.0, sampleCount);
        }

        public static OrbitMapVector3[] SamplePatchInPrimaryFrame(OrbitMapPatch patch, OrbitMapBody referenceBody, double stateUniversalTimeSeconds, int sampleCount)
        {
            return SamplePatchInPrimaryFrameAligned(patch, referenceBody, stateUniversalTimeSeconds, sampleCount, new OrbitMapVector3(), false);
        }

        public static OrbitMapVector3[] SamplePatchInPrimaryFrameAligned(OrbitMapPatch patch, OrbitMapBody referenceBody, double stateUniversalTimeSeconds, int sampleCount, OrbitMapVector3 continuityAnchor, bool haveContinuityAnchor)
        {
            if (patch == null || referenceBody == null) return new OrbitMapVector3[0];

            if (patch.Orbit != null && patch.Orbit.Eccentricity > 1.0 &&
                referenceBody.SoiRadiusMeters > 0.0 && referenceBody.GravParameter > 0.0 &&
                !double.IsNaN(patch.EndUniversalTimeSeconds) && !double.IsInfinity(patch.EndUniversalTimeSeconds) &&
                patch.EndUniversalTimeSeconds > patch.StartUniversalTimeSeconds)
            {
                OrbitMapVector3[] soiAnchored = SampleHyperbolicPatchFromSoiBoundary(
                    patch, referenceBody, stateUniversalTimeSeconds, sampleCount, continuityAnchor, haveContinuityAnchor);
                if (soiAnchored.Length > 0) return soiAnchored;
            }

            OrbitMapVector3[] local = OrbitMapConicSampler.SamplePatch(patch, sampleCount, referenceBody.GravParameter);
            if (local.Length == 0) return new OrbitMapVector3[0];
            return TranslateLocalPatchWithMovingBody(local, patch, referenceBody, stateUniversalTimeSeconds);
        }

        private static OrbitMapVector3[] SampleHyperbolicPatchFromSoiBoundary(OrbitMapPatch patch, OrbitMapBody referenceBody, double stateUniversalTimeSeconds, int sampleCount, OrbitMapVector3 continuityAnchor, bool haveContinuityAnchor)
        {
            double entryMagnitude = TrueAnomalyAtRadius(patch.Orbit, referenceBody.SoiRadiusMeters);
            if (double.IsNaN(entryMagnitude) || double.IsInfinity(entryMagnitude)) return new OrbitMapVector3[0];

            double entryPositive = entryMagnitude;
            double entryNegative = -entryMagnitude;
            OrbitMapVector3[] positiveLocal = SampleHyperbolicBranchFromEntry(patch, referenceBody.GravParameter, sampleCount, entryPositive);
            OrbitMapVector3[] negativeLocal = SampleHyperbolicBranchFromEntry(patch, referenceBody.GravParameter, sampleCount, entryNegative);
            if (positiveLocal.Length == 0) return negativeLocal;
            if (negativeLocal.Length == 0) return positiveLocal;

            OrbitMapVector3[] positive = TranslateLocalPatchWithMovingBody(positiveLocal, patch, referenceBody, stateUniversalTimeSeconds);
            OrbitMapVector3[] negative = TranslateLocalPatchWithMovingBody(negativeLocal, patch, referenceBody, stateUniversalTimeSeconds);

            // An encounter begins on the inbound branch. Continuity with the preceding
            // parent-body patch is authoritative when available; otherwise prefer inbound.
            if (!haveContinuityAnchor) return negative;
            double positiveDistance = DistanceSquared(positive[0], continuityAnchor);
            double negativeDistance = DistanceSquared(negative[0], continuityAnchor);
            return positiveDistance < negativeDistance ? positive : negative;
        }

        private static OrbitMapVector3[] SampleHyperbolicBranchFromEntry(OrbitMapPatch patch, double gravParameter, int sampleCount, double entryTrueAnomaly)
        {
            if (patch == null || patch.Orbit == null || sampleCount < 4 || gravParameter <= 0.0) return new OrbitMapVector3[0];
            double a = patch.Orbit.SemiMajorAxisMeters;
            double e = patch.Orbit.Eccentricity;
            if (a == 0.0 || e <= 1.0) return new OrbitMapVector3[0];

            double meanMotion = Math.Sqrt(gravParameter / Math.Pow(Math.Abs(a), 3.0));
            double meanAnomalyAtEntry = MeanAnomalyFromTrueAnomalyHyperbolic(entryTrueAnomaly, e);
            if (double.IsNaN(meanAnomalyAtEntry) || double.IsInfinity(meanAnomalyAtEntry)) return new OrbitMapVector3[0];

            OrbitMapVector3[] result = new OrbitMapVector3[sampleCount + 1];
            double start = patch.StartUniversalTimeSeconds;
            double end = patch.EndUniversalTimeSeconds;
            for (int i = 0; i <= sampleCount; i++)
            {
                double ut = start + (end - start) * i / sampleCount;
                double meanAnomaly = meanAnomalyAtEntry + meanMotion * (ut - patch.StartUniversalTimeSeconds);
                double hyperbolicAnomaly = SolveHyperbolicAnomaly(meanAnomaly, e);
                double factor = Math.Sqrt((e + 1.0) / (e - 1.0));
                double trueAnomaly = 2.0 * Math.Atan(factor * Math.Tanh(hyperbolicAnomaly * 0.5));
                result[i] = OrbitMapConicSampler.PositionAtTrueAnomaly(patch.Orbit, trueAnomaly);
            }
            return result;
        }

        private static double TrueAnomalyAtRadius(OrbitMapOrbit orbit, double radiusMeters)
        {
            if (orbit == null || orbit.Eccentricity <= 1.0 || radiusMeters <= 0.0) return double.NaN;
            double p = orbit.SemiMajorAxisMeters * (1.0 - orbit.Eccentricity * orbit.Eccentricity);
            double cosNu = ((p / radiusMeters) - 1.0) / orbit.Eccentricity;
            if (cosNu < -1.0) cosNu = -1.0;
            if (cosNu > 1.0) cosNu = 1.0;
            return Math.Acos(cosNu);
        }

        private static double MeanAnomalyFromTrueAnomalyHyperbolic(double trueAnomaly, double eccentricity)
        {
            if (eccentricity <= 1.0) return double.NaN;
            double scale = Math.Sqrt((eccentricity - 1.0) / (eccentricity + 1.0));
            double tanhHalfH = scale * Math.Tan(trueAnomaly * 0.5);
            if (tanhHalfH <= -1.0 || tanhHalfH >= 1.0) return double.NaN;
            double hyperbolicAnomaly = Math.Log((1.0 + tanhHalfH) / (1.0 - tanhHalfH));
            return eccentricity * Math.Sinh(hyperbolicAnomaly) - hyperbolicAnomaly;
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

        private static OrbitMapVector3[] TranslateLocalPatchWithMovingBody(OrbitMapVector3[] local, OrbitMapPatch patch, OrbitMapBody referenceBody, double stateUniversalTimeSeconds)
        {
            OrbitMapVector3[] result = new OrbitMapVector3[local.Length];
            double start = patch.StartUniversalTimeSeconds;
            double end = patch.EndUniversalTimeSeconds;
            bool bounded = !double.IsNaN(end) && !double.IsInfinity(end) && end > start;
            for (int i = 0; i < local.Length; i++)
            {
                double ut = bounded && local.Length > 1 ? start + (end - start) * i / (local.Length - 1) : start;
                OrbitMapVector3 center = BodyPositionAtUniversalTime(referenceBody, stateUniversalTimeSeconds, ut);
                result[i] = new OrbitMapVector3(local[i].X + center.X, local[i].Y + center.Y, local[i].Z + center.Z);
            }
            return result;
        }

        private static double DistanceSquared(OrbitMapVector3 a, OrbitMapVector3 b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            double dz = a.Z - b.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        private static double TrueAnomalyFromCanonicalPosition(OrbitMapOrbit orbit, OrbitMapVector3 position)
        {
            if (orbit == null) return double.NaN;
            double w = DegreesToRadians(orbit.ArgumentOfPeriapsisDegrees);
            double inc = DegreesToRadians(orbit.InclinationDegrees);
            double lan = DegreesToRadians(orbit.LongitudeOfAscendingNodeDegrees);
            double cw = Math.Cos(w), sw = Math.Sin(w);
            double ci = Math.Cos(inc), si = Math.Sin(inc);
            double co = Math.Cos(lan), so = Math.Sin(lan);

            // Undo LAN rotation, then inclination, then argument of periapsis.
            double x2 = co * position.X + so * position.Y;
            double y2 = -so * position.X + co * position.Y;
            double z2 = position.Z;
            double x1 = x2;
            double y1 = ci * y2 + si * z2;
            double x = cw * x1 + sw * y1;
            double y = -sw * x1 + cw * y1;
            if (Math.Abs(x) < 1e-12 && Math.Abs(y) < 1e-12) return double.NaN;
            return NormalizeRadians(Math.Atan2(y, x));
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

        private static double NormalizeRadians(double radians)
        {
            double twoPi = 2.0 * Math.PI;
            radians %= twoPi;
            if (radians < 0.0) radians += twoPi;
            return radians;
        }

        private static double DegreesToRadians(double degrees) { return degrees * Math.PI / 180.0; }
    }
}
