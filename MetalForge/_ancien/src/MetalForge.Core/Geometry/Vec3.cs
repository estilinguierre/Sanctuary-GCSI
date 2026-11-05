using System;
using System.Globalization;

namespace MetalForge.Core.Geometry
{
    /// <summary>Point / vecteur 3D en millimètres. Repère : X = longueur, Y = hauteur, Z = profondeur.</summary>
    public readonly struct Vec3 : IEquatable<Vec3>
    {
        public readonly double X, Y, Z;

        public Vec3(double x, double y, double z) { X = x; Y = y; Z = z; }

        public static readonly Vec3 Zero = new Vec3(0, 0, 0);

        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

        public Vec3 Normalized()
        {
            var l = Length;
            return l < 1e-12 ? Zero : new Vec3(X / l, Y / l, Z / l);
        }

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator *(Vec3 a, double k) => new Vec3(a.X * k, a.Y * k, a.Z * k);

        public static double Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Vec3 Cross(Vec3 a, Vec3 b) =>
            new Vec3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        public bool Equals(Vec3 o) => Math.Abs(X - o.X) < 1e-6 && Math.Abs(Y - o.Y) < 1e-6 && Math.Abs(Z - o.Z) < 1e-6;
        public override bool Equals(object obj) => obj is Vec3 v && Equals(v);
        public override int GetHashCode() => (Math.Round(X, 3), Math.Round(Y, 3), Math.Round(Z, 3)).GetHashCode();

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "({0:0.##}; {1:0.##}; {2:0.##})", X, Y, Z);
    }
}
