using System;
using KMC.Shared;

namespace KMC.MissionControl.Rendering.OrbitMap
{
    public static class OrbitMapConicSampler
    {
        public static OrbitMapVector3[] SampleClosedOrbit(OrbitMapOrbit orbit, int sampleCount)
        {
            if (orbit == null || sampleCount < 4 || orbit.Eccentricity >= 1.0) return new OrbitMapVector3[0];
            OrbitMapVector3[] result = new OrbitMapVector3[sampleCount + 1];
            for (int i = 0; i <= sampleCount; i++)
            {
                double nu = 2.0 * Math.PI * i / sampleCount;
                result[i] = PositionAtTrueAnomaly(orbit, nu);
            }
            return result;
        }

        public static OrbitMapVector3[] SamplePatch(OrbitMapPatch patch, int sampleCount)
        {
            if (patch == null || patch.Orbit == null || sampleCount < 4) return new OrbitMapVector3[0];

            if (patch.Orbit.Eccentricity < 1.0 &&
                !double.IsNaN(patch.Orbit.PeriodSeconds) && !double.IsInfinity(patch.Orbit.PeriodSeconds) &&
                patch.Orbit.PeriodSeconds > 0.0)
            {
                double startUt = patch.StartUniversalTimeSeconds;
                double duration = patch.Orbit.PeriodSeconds;
                double endUt = patch.EndUniversalTimeSeconds;
                if (!double.IsNaN(endUt) && !double.IsInfinity(endUt) && endUt > startUt)
                    duration = Math.Min(duration, endUt - startUt);

                OrbitMapVector3[] bounded = new OrbitMapVector3[sampleCount + 1];
                for (int i = 0; i <= sampleCount; i++)
                {
                    double ut = startUt + duration * i / sampleCount;
                    bounded[i] = PositionAtUniversalTime(patch.Orbit, ut);
                }
                return bounded;
            }

            // Open conics are intentionally bounded. KSP owns the exact patch
            // time limits; this display samples the physically useful branch
            // around periapsis and never loops a hyperbola through 2*pi.
            double e = patch.Orbit.Eccentricity;
            double limit = Math.Acos(-1.0 / e) - 0.02;
            if (double.IsNaN(limit) || double.IsInfinity(limit) || limit <= 0.0) limit = 2.5;
            OrbitMapVector3[] points = new OrbitMapVector3[sampleCount + 1];
            for (int i = 0; i <= sampleCount; i++)
            {
                double nu = -limit + (2.0 * limit * i / sampleCount);
                points[i] = PositionAtTrueAnomaly(patch.Orbit, nu);
            }
            return points;
        }

        public static OrbitMapVector3 PositionAtUniversalTime(OrbitMapOrbit orbit, double universalTimeSeconds)
        {
            if (orbit == null || orbit.Eccentricity >= 1.0 ||
                double.IsNaN(orbit.PeriodSeconds) || double.IsInfinity(orbit.PeriodSeconds) || orbit.PeriodSeconds <= 0.0)
                return new OrbitMapVector3();

            double meanMotion = 2.0 * Math.PI / orbit.PeriodSeconds;
            double meanAnomaly = NormalizeRadians(orbit.MeanAnomalyAtEpochRadians + meanMotion * (universalTimeSeconds - orbit.EpochUniversalTimeSeconds));
            double eccentricAnomaly = SolveEccentricAnomaly(meanAnomaly, orbit.Eccentricity);
            double sinHalf = Math.Sqrt(1.0 + orbit.Eccentricity) * Math.Sin(eccentricAnomaly * 0.5);
            double cosHalf = Math.Sqrt(1.0 - orbit.Eccentricity) * Math.Cos(eccentricAnomaly * 0.5);
            double trueAnomaly = NormalizeRadians(2.0 * Math.Atan2(sinHalf, cosHalf));
            return PositionAtTrueAnomaly(orbit, trueAnomaly);
        }

        public static OrbitMapVector3 PositionAtTrueAnomaly(OrbitMapOrbit orbit, double trueAnomalyRadians)
        {
            if (orbit == null) return new OrbitMapVector3();
            double a = orbit.SemiMajorAxisMeters;
            double e = orbit.Eccentricity;
            double p = a * (1.0 - e * e);
            double denom = 1.0 + e * Math.Cos(trueAnomalyRadians);
            if (Math.Abs(denom) < 1e-12) denom = denom < 0.0 ? -1e-12 : 1e-12;
            double r = p / denom;
            double x = r * Math.Cos(trueAnomalyRadians);
            double y = r * Math.Sin(trueAnomalyRadians);

            double w = DegreesToRadians(orbit.ArgumentOfPeriapsisDegrees);
            double inc = DegreesToRadians(orbit.InclinationDegrees);
            double lan = DegreesToRadians(orbit.LongitudeOfAscendingNodeDegrees);

            double cw = Math.Cos(w), sw = Math.Sin(w);
            double ci = Math.Cos(inc), si = Math.Sin(inc);
            double co = Math.Cos(lan), so = Math.Sin(lan);

            double x1 = cw * x - sw * y;
            double y1 = sw * x + cw * y;
            double x2 = x1;
            double y2 = ci * y1;
            double z2 = si * y1;
            return new OrbitMapVector3(co * x2 - so * y2, so * x2 + co * y2, z2);
        }

        public static OrbitMapVector3 ApoapsisPosition(OrbitMapOrbit orbit) { return PositionAtTrueAnomaly(orbit, Math.PI); }
        public static OrbitMapVector3 PeriapsisPosition(OrbitMapOrbit orbit) { return PositionAtTrueAnomaly(orbit, 0.0); }

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
