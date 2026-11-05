using System;
using System.Globalization;

namespace MetalForge.Core.Profiles
{
    public enum ProfileFamily
    {
        TubeCarre,        // EN 10219 / EN 10210
        TubeRectangulaire,
        TubeRond,
        Plat,             // EN 10058
        CarrePlein,       // EN 10059
        RondPlein,        // EN 10060
        Corniere,         // EN 10056 (ailes égales)
        UPN,              // DIN 1026
        IPE,              // EN 10365
        HEA,              // EN 10365
        FerT,             // EN 10055
        HEB,              // EN 10365
        Cable             // câble inox 7x19
    }

    /// <summary>
    /// Section de profilé. Toutes les cotes sont en mm.
    /// H = hauteur (selon Y local), B = largeur, T = épaisseur (ou âme), Tf = semelle.
    /// </summary>
    public sealed class Profile
    {
        public const double SteelDensity = 7850.0; // kg/m³ (S235 / S355)

        public string Id { get; set; }
        public ProfileFamily Family { get; set; }
        public double H { get; set; }
        public double B { get; set; }
        public double T { get; set; }
        public double Tf { get; set; }

        /// <summary>Masse linéique tabulée (kg/m). Si 0, elle est calculée depuis la section.</summary>
        public double TabulatedMassPerMeter { get; set; }

        public string Designation
        {
            get
            {
                string f(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);
                switch (Family)
                {
                    case ProfileFamily.TubeCarre: return $"Tube carré {f(B)}x{f(H)}x{f(T)}";
                    case ProfileFamily.TubeRectangulaire: return $"Tube rect. {f(H)}x{f(B)}x{f(T)}";
                    case ProfileFamily.TubeRond: return $"Tube rond Ø{f(B)}x{f(T)}";
                    case ProfileFamily.Plat: return $"Plat {f(B)}x{f(T)}";
                    case ProfileFamily.CarrePlein: return $"Carré plein {f(B)}";
                    case ProfileFamily.RondPlein: return $"Rond plein Ø{f(B)}";
                    case ProfileFamily.Corniere: return $"Cornière {f(B)}x{f(H)}x{f(T)}";
                    case ProfileFamily.FerT: return $"Fer T {f(B)}x{f(H)}x{f(T)}";
                    case ProfileFamily.Cable: return $"Câble inox Ø{f(B)}";
                    default: return Id; // UPN 120, IPE 200, HEA 140...
                }
            }
        }

        /// <summary>Aire de section (mm²). Formules simplifiées (rayons de raccordement négligés sauf tubes EN 10219).</summary>
        public double Area
        {
            get
            {
                switch (Family)
                {
                    case ProfileFamily.TubeCarre:
                    case ProfileFamily.TubeRectangulaire:
                        // EN 10219-2 : ro = 2t, ri = t (t <= 6 mm) => A = 2t(B+H-8t) + 3πt²
                        return 2 * T * (B + H - 8 * T) + 3 * Math.PI * T * T;
                    case ProfileFamily.TubeRond: return Math.PI * (B - T) * T;
                    case ProfileFamily.Plat: return B * T;
                    case ProfileFamily.CarrePlein: return B * B;
                    case ProfileFamily.RondPlein:
                    case ProfileFamily.Cable: return Math.PI * B * B / 4.0;
                    case ProfileFamily.Corniere: return T * (B + H - T);
                    case ProfileFamily.FerT: return T * (B + H - T);
                    case ProfileFamily.UPN:
                    case ProfileFamily.IPE:
                    case ProfileFamily.HEA:
                    case ProfileFamily.HEB:
                        return 2 * B * Tf + (H - 2 * Tf) * T;
                    default: return 0;
                }
            }
        }

        /// <summary>Masse linéique (kg/m).</summary>
        public double MassPerMeter => TabulatedMassPerMeter > 0 ? TabulatedMassPerMeter : Area * 1e-6 * SteelDensity;

        /// <summary>Périmètre extérieur (mm) : surface à peindre / galvaniser par mètre linéaire.</summary>
        public double PaintPerimeter
        {
            get
            {
                switch (Family)
                {
                    case ProfileFamily.TubeCarre:
                    case ProfileFamily.TubeRectangulaire: return 2 * (B + H);
                    case ProfileFamily.TubeRond:
                    case ProfileFamily.RondPlein:
                    case ProfileFamily.Cable: return Math.PI * B;
                    case ProfileFamily.Plat: return 2 * (B + T);
                    case ProfileFamily.CarrePlein: return 4 * B;
                    case ProfileFamily.Corniere:
                    case ProfileFamily.FerT: return 2 * (B + H);
                    case ProfileFamily.UPN: return 2 * H + 4 * B - 2 * T;
                    case ProfileFamily.IPE:
                    case ProfileFamily.HEA:
                    case ProfileFamily.HEB: return 2 * H + 4 * B - 2 * T;
                    default: return 0;
                }
            }
        }

        /// <summary>Encombrement dans le plan de la vue (largeur vue de face), utile au calcul des vides.</summary>
        public double VisibleWidth => B;

        /// <summary>Encombrement transversal (selon Z) d'une barre posée « debout » : épaisseur pour un plat, largeur sinon.</summary>
        public double WidthAcross => Family == ProfileFamily.Plat ? T : B;

        /// <summary>Encombrement vertical (hauteur de section).</summary>
        public double SectionHeight
        {
            get
            {
                switch (Family)
                {
                    case ProfileFamily.TubeRond:
                    case ProfileFamily.RondPlein:
                    case ProfileFamily.Cable:
                    case ProfileFamily.CarrePlein: return B;
                    case ProfileFamily.Plat: return B;
                    default: return H;
                }
            }
        }

        public override string ToString() => Designation;
    }
}
