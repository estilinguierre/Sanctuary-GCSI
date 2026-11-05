using System;
using System.Linq;

namespace MetalForge.Core.Profiles
{
    /// <summary>
    /// Caractéristiques mécaniques d'une section (mm⁴, mm³).
    /// Axe « y » : flexion dans le plan de la hauteur de section (SectionHeight) — profil « debout ».
    /// Axe « z » : flexion perpendiculaire (axe faible pour un plat, un UPN, un IPE…).
    /// Rayons de raccordement négligés : valeurs légèrement inférieures aux catalogues (sécuritaire).
    /// </summary>
    public readonly struct SectionProperties
    {
        public readonly double Iy, Iz, Wy, Wz;
        /// <summary>Modules plastiques (sections compactes, classe 1-2). Sections ouvertes : = module élastique (sécuritaire).</summary>
        public readonly double WplY, WplZ;
        public SectionProperties(double iy, double iz, double wy, double wz, double wply = 0, double wplz = 0)
        { Iy = iy; Iz = iz; Wy = wy; Wz = wz; WplY = wply > 0 ? wply : wy; WplZ = wplz > 0 ? wplz : wz; }
        public double Imin => Math.Min(Iy, Iz);
        public double Wmin => Math.Min(Wy, Wz);
        public double WplMin => Math.Min(WplY, WplZ);

        public static SectionProperties Of(Profile p)
        {
            switch (p.Family)
            {
                case ProfileFamily.TubeCarre:
                case ProfileFamily.TubeRectangulaire:
                {
                    double b = p.B, h = p.H, t = p.T;
                    double iy = (b * h * h * h - (b - 2 * t) * Math.Pow(h - 2 * t, 3)) / 12;
                    double iz = (h * b * b * b - (h - 2 * t) * Math.Pow(b - 2 * t, 3)) / 12;
                    double wply = (b * h * h - (b - 2 * t) * Math.Pow(h - 2 * t, 2)) / 4;
                    double wplz = (h * b * b - (h - 2 * t) * Math.Pow(b - 2 * t, 2)) / 4;
                    return new SectionProperties(iy, iz, iy / (h / 2), iz / (b / 2), wply, wplz);
                }
                case ProfileFamily.TubeRond:
                {
                    double d = p.B, di = p.B - 2 * p.T;
                    double i = Math.PI * (Math.Pow(d, 4) - Math.Pow(di, 4)) / 64;
                    double wpl = (Math.Pow(d, 3) - Math.Pow(di, 3)) / 6;
                    return new SectionProperties(i, i, i / (d / 2), i / (d / 2), wpl, wpl);
                }
                case ProfileFamily.RondPlein:
                case ProfileFamily.Cable:
                {
                    double i = Math.PI * Math.Pow(p.B, 4) / 64;
                    return new SectionProperties(i, i, i / (p.B / 2), i / (p.B / 2), Math.Pow(p.B, 3) / 6, Math.Pow(p.B, 3) / 6);
                }
                case ProfileFamily.CarrePlein:
                {
                    double i = Math.Pow(p.B, 4) / 12;
                    return new SectionProperties(i, i, i / (p.B / 2), i / (p.B / 2), Math.Pow(p.B, 3) / 4, Math.Pow(p.B, 3) / 4);
                }
                case ProfileFamily.Plat:
                {
                    // debout : largeur B verticale
                    double iy = p.T * Math.Pow(p.B, 3) / 12, iz = p.B * Math.Pow(p.T, 3) / 12;
                    return new SectionProperties(iy, iz, iy / (p.B / 2), iz / (p.T / 2), p.T * p.B * p.B / 4, p.B * p.T * p.T / 4);
                }
                case ProfileFamily.Corniere:
                    // aile horizontale B x T en haut, aile verticale T x (H-T)
                    return Composite((0, 0, p.B, p.T), (0, p.T, p.T, p.H - p.T));
                case ProfileFamily.FerT:
                    return Composite((0, 0, p.B, p.T), ((p.B - p.T) / 2, p.T, p.T, p.H - p.T));
                case ProfileFamily.UPN:
                    return Composite((0, 0, p.B, p.Tf), (0, p.Tf, p.T, p.H - 2 * p.Tf), (0, p.H - p.Tf, p.B, p.Tf));
                case ProfileFamily.IPE:
                case ProfileFamily.HEA:
                case ProfileFamily.HEB:
                    return Composite((0, 0, p.B, p.Tf), ((p.B - p.T) / 2, p.Tf, p.T, p.H - 2 * p.Tf), (0, p.H - p.Tf, p.B, p.Tf));
                case ProfileFamily.Special:
                {   // approximation : rectangle plein de même aire
                    double h = Math.Max(p.H, 1), w = p.Area / h, iy = w * h * h * h / 12, iz = h * w * w * w / 12;
                    return new SectionProperties(iy, iz, iy / (h / 2), iz / (w / 2));
                }
                default:
                    return new SectionProperties(0, 0, 0, 0);
            }
        }

        /// <summary>Section composée de rectangles (x, y, largeur, hauteur) — y vers le bas, axe y = horizontal.</summary>
        public static SectionProperties Composite(params (double X, double Y, double W, double H)[] r)
        {
            double a = r.Sum(q => q.W * q.H);
            double cx = r.Sum(q => q.W * q.H * (q.X + q.W / 2)) / a;
            double cy = r.Sum(q => q.W * q.H * (q.Y + q.H / 2)) / a;
            double iy = r.Sum(q => q.W * Math.Pow(q.H, 3) / 12 + q.W * q.H * Math.Pow(q.Y + q.H / 2 - cy, 2));
            double iz = r.Sum(q => q.H * Math.Pow(q.W, 3) / 12 + q.W * q.H * Math.Pow(q.X + q.W / 2 - cx, 2));
            double vy = r.Max(q => Math.Max(Math.Abs(q.Y - cy), Math.Abs(q.Y + q.H - cy)));
            double vz = r.Max(q => Math.Max(Math.Abs(q.X - cx), Math.Abs(q.X + q.W - cx)));
            return new SectionProperties(iy, iz, iy / vy, iz / vz);
        }
    }
}
