using System;

namespace SwFeatureDebug
{
    internal struct Point3
    {
        public double X;
        public double Y;
        public double Z;

        public Point3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double DistanceTo(Point3 other)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            double dz = Z - other.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public string ToMillimeterText()
        {
            return "X=" + (X * 1000.0).ToString("F3") + " mm, Y=" + (Y * 1000.0).ToString("F3") + " mm, Z=" + (Z * 1000.0).ToString("F3") + " mm";
        }
    }
}