using System;

namespace KMC.Engine.CelestialMechanics
{
    /// <summary>Unperturbed two-body propagation from elements to an inertial state.
    /// No SOI changes, thrust, atmospheric effects or raw Unity frame conversion.
    /// Finite-a elements cannot describe e=1; exact parabolas are explicitly unsupported.</summary>
    public static class KeplerPropagator
    {
        public static bool TryPropagate(OrbitalElements orbit, double mu, double ut, out StateVector state)
        {
            state = null;
            if (orbit == null || !orbit.HasFiniteGeometry || !Vector3d.Finite(mu) || mu <= 0 ||
                !Vector3d.Finite(ut)) return false;
            double a = orbit.SemiMajorAxisMeters, e = orbit.Eccentricity;
            if (e < 0 || e == 1 || a == 0 || (e < 1 ? a < 0 : a > 0)) return false;
            double size = Math.Abs(a);
            double n = Math.Sqrt(mu / size) / size;
            double mean = orbit.MeanAnomalyAtEpochRadians + n * (ut - orbit.EpochUniversalTimeSeconds);
            if (!Vector3d.Finite(n) || n <= 0 || !Vector3d.Finite(mean)) return false;

            double anomaly;
            if (!TrySolveAnomaly(mean, e, out anomaly)) return false;
            double x, y, vx, vy;
            if (e < 1)
            {
                double c = Math.Cos(anomaly), s = Math.Sin(anomaly), beta = Math.Sqrt((1 - e) * (1 + e));
                double factor = Math.Sqrt(mu / size) / (1 - e * c);
                x = a * (c - e); y = a * beta * s;
                vx = -factor * s; vy = factor * beta * c;
            }
            else
            {
                double c = Math.Cosh(anomaly), s = Math.Sinh(anomaly), beta = Math.Sqrt((e - 1) * (e + 1));
                double factor = Math.Sqrt(mu / size) / (e * c - 1);
                x = size * (e - c); y = size * beta * s;
                vx = -factor * s; vy = factor * beta * c;
            }
            Vector3d position = Rotate(orbit, x, y), velocity = Rotate(orbit, vx, vy);
            if (!position.IsFinite || !velocity.IsFinite || !Vector3d.Finite(position.Magnitude) || position.Magnitude <= 0) return false;
            state = new StateVector(position, velocity, ut, mu, orbit.ReferenceBodyName);
            return true;
        }

        private static bool TrySolveAnomaly(double mean, double e, out double anomaly)
        {
            anomaly = 0;
            bool elliptic = e < 1;
            if (elliptic)
            {
                mean %= 2 * Math.PI;
                if (mean > Math.PI) mean -= 2 * Math.PI;
                if (mean < -Math.PI) mean += 2 * Math.PI;
            }
            if (mean == 0) return true;
            double sign = mean < 0 ? -1 : 1;
            double target = Math.Abs(mean);
            double low = 0, high = elliptic ? Math.PI : Math.Max(1, Math.Log(target) + 2);
            // Both equations are monotone for positive anomaly. Bisection cannot
            // escape the bracket near e=1, unlike an unguarded Newton iteration.
            if (!elliptic)
            {
                while (e * Math.Sinh(high) - high < target && high < 700) high *= 2;
                if (high > 700 || !Vector3d.Finite(e * Math.Sinh(high))) return false;
            }
            for (int i = 0; i < 160; i++)
            {
                double mid = low + .5 * (high - low);
                double value = elliptic ? mid - e * Math.Sin(mid) : e * Math.Sinh(mid) - mid;
                if (!Vector3d.Finite(value)) return false;
                if (value < target) low = mid; else high = mid;
                if (high - low <= 2e-15 * Math.Max(1, Math.Abs(mid)))
                {
                    anomaly = sign * (low + .5 * (high - low));
                    return true;
                }
            }
            return false;
        }

        private static Vector3d Rotate(OrbitalElements o, double x, double y)
        {
            double w = (o.ArgumentOfPeriapsisDegrees % 360) * Math.PI / 180;
            double i = (o.InclinationDegrees % 360) * Math.PI / 180;
            double node = (o.LongitudeOfAscendingNodeDegrees % 360) * Math.PI / 180;
            double x1 = Math.Cos(w) * x - Math.Sin(w) * y;
            double y1 = Math.Sin(w) * x + Math.Cos(w) * y;
            double y2 = Math.Cos(i) * y1, z2 = Math.Sin(i) * y1;
            return new Vector3d(Math.Cos(node) * x1 - Math.Sin(node) * y2,
                Math.Sin(node) * x1 + Math.Cos(node) * y2, z2);
        }
    }
}
