using System;

namespace KMC.Engine.CelestialMechanics
{
    /// <summary>Double-precision vector in a declared right-handed reference frame.</summary>
    public struct Vector3d
    {
        public readonly double X, Y, Z;
        public Vector3d(double x, double y, double z) { X = x; Y = y; Z = z; }
        public double Magnitude { get { return Math.Sqrt(Dot(this, this)); } }
        public bool IsFinite { get { return Finite(X) && Finite(Y) && Finite(Z); } }
        public static double Dot(Vector3d a, Vector3d b) { return a.X * b.X + a.Y * b.Y + a.Z * b.Z; }
        public static Vector3d Cross(Vector3d a, Vector3d b)
        { return new Vector3d(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X); }
        public static Vector3d operator +(Vector3d a, Vector3d b) { return new Vector3d(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }
        public static Vector3d operator -(Vector3d a, Vector3d b) { return new Vector3d(a.X - b.X, a.Y - b.Y, a.Z - b.Z); }
        public static Vector3d operator *(Vector3d a, double scale) { return new Vector3d(a.X * scale, a.Y * scale, a.Z * scale); }
        internal static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
    }
}
