using System;

namespace KMC.MissionControl.Rendering.OrbitMap
{
    public struct OrbitMapVector3
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Z;
        public OrbitMapVector3(double x, double y, double z) { X = x; Y = y; Z = z; }
        public double Magnitude { get { return Math.Sqrt(X * X + Y * Y + Z * Z); } }
    }
}
