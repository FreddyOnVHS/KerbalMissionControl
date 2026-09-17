using System;
using System.Drawing;

namespace KMC.MissionControl.Rendering.OrbitMap
{
    public sealed class OrbitMapCamera
    {
        private const double FieldOfViewRadians = 45.0 * Math.PI / 180.0;
        private double _yawRadians;
        private double _pitchRadians;
        private double _distance;

        public OrbitMapCamera() { Reset(); }
        public double Distance { get { return _distance; } }
        public double YawDegrees
        {
            get
            {
                double normalized = NormalizeDegrees(_yawRadians * 180.0 / Math.PI);
                return normalized < 1e-9 ? 360.0 : normalized;
            }
        }
        public double PitchDegrees { get { return _pitchRadians * 180.0 / Math.PI; } }

        public void Rotate(float deltaX, float deltaY)
        {
            _yawRadians += deltaX * 0.008;
            _pitchRadians += deltaY * 0.008;
            double limit = 89.0 * Math.PI / 180.0;
            if (_pitchRadians > limit) _pitchRadians = limit;
            if (_pitchRadians < -limit) _pitchRadians = -limit;
        }

        public void Zoom(int wheelDelta)
        {
            double factor = Math.Pow(0.90, wheelDelta / 120.0);
            _distance *= factor;
            if (_distance < 100.0) _distance = 100.0;
            if (_distance > 1.0e14) _distance = 1.0e14;
        }

        public void Reset()
        {
            _yawRadians = 0.0;
            _pitchRadians = 0.0;
            _distance = 4000000.0;
        }

        public void Fit(double sceneRadius)
        {
            if (double.IsNaN(sceneRadius) || double.IsInfinity(sceneRadius) || sceneRadius <= 1.0) return;
            _distance = Math.Max(100.0, Math.Min(1.0e14, sceneRadius * 2.8));
        }

        public bool TryProject(OrbitMapVector3 world, Rectangle viewport, out PointF screen, out double depth)
        {
            double x2, y2, z2;
            TransformToCamera(world, out x2, out y2, out z2);
            depth = _distance - z2;
            if (depth <= 1.0 || viewport.Width <= 0 || viewport.Height <= 0)
            {
                screen = PointF.Empty;
                return false;
            }
            double focal = (viewport.Height * 0.5) / Math.Tan(FieldOfViewRadians * 0.5);
            screen = new PointF(
                (float)(viewport.Left + viewport.Width * 0.5 + x2 * focal / depth),
                (float)(viewport.Top + viewport.Height * 0.5 - y2 * focal / depth));
            return true;
        }

        public bool IsOccludedBySphere(OrbitMapVector3 world, double sphereRadius)
        {
            if (sphereRadius <= 0.0 || double.IsNaN(sphereRadius) || double.IsInfinity(sphereRadius)) return false;

            double px, py, pz;
            TransformToCamera(world, out px, out py, out pz);

            double cx = 0.0, cy = 0.0, cz = _distance;
            double dx = px - cx, dy = py - cy, dz = pz - cz;
            double a = dx * dx + dy * dy + dz * dz;
            if (a <= 1e-12) return false;

            double b = 2.0 * (cx * dx + cy * dy + cz * dz);
            double c = cx * cx + cy * cy + cz * cz - sphereRadius * sphereRadius;
            double discriminant = b * b - 4.0 * a * c;
            if (discriminant <= 0.0) return false;

            double root = Math.Sqrt(discriminant);
            double t0 = (-b - root) / (2.0 * a);
            double t1 = (-b + root) / (2.0 * a);
            const double epsilon = 1e-6;
            return (t0 > epsilon && t0 < 1.0 - epsilon) ||
                   (t1 > epsilon && t1 < 1.0 - epsilon);
        }

        public PointF ProjectDirection(OrbitMapVector3 worldDirection)
        {
            double x, y, z;
            TransformDirection(worldDirection, out x, out y, out z);
            double length = Math.Sqrt(x * x + y * y);
            if (length <= 1e-9) return PointF.Empty;
            return new PointF((float)(x / length), (float)(-y / length));
        }

        private void TransformToCamera(OrbitMapVector3 world, out double x, out double y, out double z)
        {
            double cy = Math.Cos(_yawRadians), sy = Math.Sin(_yawRadians);
            double cp = Math.Cos(_pitchRadians), sp = Math.Sin(_pitchRadians);
            double x1 = cy * world.X - sy * world.Y;
            double y1 = sy * world.X + cy * world.Y;
            x = x1;
            y = cp * y1 - sp * world.Z;
            z = sp * y1 + cp * world.Z;
        }

        private void TransformDirection(OrbitMapVector3 direction, out double x, out double y, out double z)
        {
            TransformToCamera(direction, out x, out y, out z);
        }

        private static double NormalizeDegrees(double degrees)
        {
            degrees %= 360.0;
            if (degrees < 0.0) degrees += 360.0;
            return degrees;
        }
    }
}
