using System;
using System.Collections.Generic;
using System.ComponentModel;
using MetalForge.Core.Geometry;
using MetalForge.Core.Model;
using MetalForge.Core.Profiles;
using MetalForge.Core.Standards;
using MetalForge.Core.Structural;

namespace MetalForge.Core.Ladders
{
    [TypeConverter(typeof(EnumDescriptionConverter))]
    public enum LadderUse
    {
        [Description("Bâtiment (NF E85-016)")] Batiment,
        [Description("Machine (NF EN ISO 14122-4)")] Machine
    }

    /// <summary>Échelle fixe à crinoline. Repère : X = largeur, Y = hauteur, Z = 0 plan des montants, mur en Z négatif.</summary>
    public sealed class LadderParameters
    {
        [Category("1. Implantation"), DisplayName("Usage")] public LadderUse Use { get; set; } = LadderUse.Batiment;
        [Category("1. Implantation"), DisplayName("Hauteur à franchir (mm)")] public double ClimbHeight { get; set; } = 6000;
        [Category("1. Implantation"), DisplayName("Dépassement des montants (mm)"),
         Description("Au-dessus du niveau d'arrivée, pour la prise en main (1100 mm usuel, à confirmer).")]
        public double StileExtension { get; set; } = 1100;
        [Category("1. Implantation"), DisplayName("Dégagement mur / échelon (mm)")] public double WallClearance { get; set; } = 200;
        [Category("1. Implantation"), DisplayName("Pattes de fixation : espacement max (mm)")] public double BracketSpacing { get; set; } = 2000;

        [Category("2. Échelle"), DisplayName("Largeur entre montants (mm)"), Description("400 à 600 mm.")] public double Width { get; set; } = 450;
        [Category("2. Échelle"), DisplayName("Pas d'échelon visé (mm)"), Description("225 à 300 mm, constant.")] public double TargetPitch { get; set; } = 280;
        [Category("2. Échelle"), DisplayName("Profil montant")] public string StileProfile { get; set; } = "PL60x10";
        [Category("2. Échelle"), DisplayName("Profil échelon")] public string RungProfile { get; set; } = "TC25x25x2";
        [Category("2. Échelle"), DisplayName("Profil patte murale")] public string BracketProfile { get; set; } = "PL50x8";

        [Category("3. Crinoline"), DisplayName("Crinoline")] public bool Cage { get; set; } = true;
        [Category("3. Crinoline"), DisplayName("Départ 1er arceau (mm)"), Description("2200 à 3000 mm du sol.")] public double CageStart { get; set; } = 2500;
        [Category("3. Crinoline"), DisplayName("Espacement max arceaux (mm)"), Description("≤ 1500 mm.")] public double HoopSpacing { get; set; } = 1200;
        [Category("3. Crinoline"), DisplayName("Diamètre intérieur (mm)"), Description("≈ 700 mm.")] public double CageDiameter { get; set; } = 700;
        [Category("3. Crinoline"), DisplayName("Espacement max filants (mm)"), Description("< 300 mm.")] public double MaxBarSpacing { get; set; } = 280;
        [Category("3. Crinoline"), DisplayName("Profil arceau")] public string HoopProfile { get; set; } = "PL40x5";
        [Category("3. Crinoline"), DisplayName("Profil filant")] public string BarProfile { get; set; } = "PL40x5";

        [Category("4. Résistance"), DisplayName("Nuance acier")] public SteelGrade Grade { get; set; } = SteelGrade.S235;
        [Category("4. Résistance"), DisplayName("Charge sur échelon (kN)"), Description("Charge ponctuelle au milieu (hypothèse 1,5 kN, à confirmer).")]
        public double RungLoad { get; set; } = 1.5;
    }

    public sealed class LadderDesigner : IDesigner
    {
        public LadderParameters P { get; }
        public LadderDesigner(LadderParameters p = null) { P = p ?? new LadderParameters(); }
        public string Title => "Échelle à crinoline";
        public object Parameters => P;

        public int Rungs { get; private set; }
        public double Pitch { get; private set; }
        public int Hoops { get; private set; }
        public double ActualHoopSpacing { get; private set; }
        public double ActualBarSpacing { get; private set; }

        public DesignResult Compute()
        {
            if (P.ClimbHeight <= 0 || P.Width <= 0) throw new ArgumentException("Hauteur et largeur doivent être positives.");
            var stile = ProfileCatalog.Get(P.StileProfile);
            var rung = ProfileCatalog.Get(P.RungProfile);
            var m = new StructureModel { Name = $"Échelle {(P.Cage ? "à crinoline " : "")}H{P.ClimbHeight:0}" };

            double sw = stile.WidthAcross;
            double xL = -sw / 2, xR = P.Width + sw / 2;
            double top = P.ClimbHeight + P.StileExtension;

            // Montants et échelons à pas constant ; le dernier échelon est au niveau d'arrivée
            m.AddMember("MO", "Montant", P.StileProfile, new Vec3(xL, 0, 0), new Vec3(xL, top, 0), 90);
            m.AddMember("MO", "Montant", P.StileProfile, new Vec3(xR, 0, 0), new Vec3(xR, top, 0), 90);
            Rungs = Math.Max(1, (int)Math.Round(P.ClimbHeight / P.TargetPitch));
            Pitch = P.ClimbHeight / Rungs;
            for (int k = 1; k <= Rungs; k++)
                m.AddMember("E", "Échelon", P.RungProfile, new Vec3(0, k * Pitch, 0), new Vec3(P.Width, k * Pitch, 0));

            // Pattes de fixation murales (règle de l'art : ≤ 2 m, plus une en tête)
            int nb = Math.Max(2, (int)Math.Ceiling(P.ClimbHeight / P.BracketSpacing) + 1);
            for (int k = 0; k < nb; k++)
            {
                double y = 300 + k * (P.ClimbHeight - 300) / (nb - 1);
                foreach (var x in new[] { xL, xR })
                    m.AddMember("PF", "Patte murale", P.BracketProfile, new Vec3(x, y, 0), new Vec3(x, y, -P.WallClearance));
            }

            if (P.Cage) BuildCage(m, xL, xR, top);

            var r = new DesignResult { Model = m };
            r.Add("Nombre d'échelons", Rungs.ToString());
            r.Add("Pas des échelons", $"{Pitch:0.0} mm");
            r.Add("Hauteur des montants", $"{top:0} mm");
            if (P.Cage)
            {
                r.Add("Nombre d'arceaux", Hoops.ToString());
                r.Add("Espacement arceaux", $"{ActualHoopSpacing:0} mm");
                r.Add("Espacement filants", $"{ActualBarSpacing:0} mm");
            }
            r.Checks.AddRange(Verify());
            return r;
        }

        private void BuildCage(StructureModel m, double xL, double xR, double top)
        {
            double R = P.CageDiameter / 2, c = (xR - xL) / 2, cx = (xL + xR) / 2;
            if (R <= c) throw new ArgumentException("Diamètre de crinoline trop petit pour la largeur d'échelle.");
            double zc = Math.Sqrt(R * R - c * c);                  // centre côté opposé au mur
            double a0 = Math.Atan2(-zc, c), a1 = Math.Atan2(-zc, -c) + 2 * Math.PI; // de l'attache droite à la gauche par l'extérieur
            const int seg = 10;
            Func<double, double, Vec3> At = (ang, y) => new Vec3(cx + R * Math.Cos(ang), y, zc + R * Math.Sin(ang));

            double start = Math.Min(P.CageStart, top);
            Hoops = Math.Max(2, (int)Math.Ceiling((top - start) / P.HoopSpacing - 1e-9) + 1);
            ActualHoopSpacing = (top - start) / (Hoops - 1);
            for (int h = 0; h < Hoops; h++)
            {
                double y = start + h * ActualHoopSpacing;
                for (int s = 0; s < seg; s++)
                    m.AddMember("A", "Arceau", P.HoopProfile, At(a0 + (a1 - a0) * s / seg, y), At(a0 + (a1 - a0) * (s + 1) / seg, y));
            }

            double arc = R * (a1 - a0);
            int nBars = Math.Max(1, (int)Math.Ceiling(arc / P.MaxBarSpacing - 1e-9) - 1);
            ActualBarSpacing = arc / (nBars + 1);
            for (int k = 1; k <= nBars; k++)
            {
                double ang = a0 + (a1 - a0) * k / (nBars + 1);
                m.AddMember("F", "Filant", P.BarProfile, At(ang, start), At(ang, top));
            }
        }

        private IEnumerable<Check> Verify()
        {
            string n = P.Use == LadderUse.Batiment ? Norms.EchelleBatiment : Norms.AccesMachines4;
            yield return Pitch >= 225 && Pitch <= 300 ? Check.Ok("Pas des échelons", $"{Pitch:0} mm (225–300)", n)
                       : Check.Fail("Pas des échelons", $"{Pitch:0} mm hors 225–300 mm", n);
            yield return P.Width >= 400 && P.Width <= 600 ? Check.Ok("Largeur entre montants", $"{P.Width:0} mm (400–600)", n)
                       : Check.Fail("Largeur entre montants", $"{P.Width:0} mm hors 400–600 mm", n);
            yield return P.WallClearance >= 200 ? Check.Ok("Dégagement au mur", $"{P.WallClearance:0} mm ≥ 200", n)
                       : Check.Fail("Dégagement au mur", $"{P.WallClearance:0} mm < 200 mm (place du pied)", n);
            if (P.Use == LadderUse.Machine)
                yield return Pitch >= 100 && Pitch <= 400 ? Check.Ok("Premier échelon", $"{Pitch:0} mm de l'aire de départ (100–400)", n)
                           : Check.Fail("Premier échelon", $"{Pitch:0} mm hors 100–400 mm", n);

            double maxFlight = P.Use == LadderUse.Batiment ? 8000 : 10000;
            yield return P.ClimbHeight <= maxFlight
                ? Check.Ok("Volée unique", $"{P.ClimbHeight:0} mm ≤ {maxFlight:0}", n)
                : Check.Fail("Volée unique", $"{P.ClimbHeight:0} mm > {maxFlight:0} mm : fractionner en volées décalées avec palier(s) de repos (usuellement tous les 6 m)", n);

            if (P.ClimbHeight > 3000 && !P.Cage)
                yield return Check.Fail("Protection contre la chute", "Hauteur > 3 m sans crinoline : prévoir crinoline ou système antichute (seuil à confirmer)", n);
            if (P.Cage)
            {
                yield return P.CageStart >= 2200 && P.CageStart <= 3000 ? Check.Ok("Départ crinoline", $"{P.CageStart:0} mm (2200–3000)", n)
                           : Check.Fail("Départ crinoline", $"{P.CageStart:0} mm hors 2200–3000 mm", n);
                yield return ActualHoopSpacing <= 1500 ? Check.Ok("Espacement arceaux", $"{ActualHoopSpacing:0} mm ≤ 1500", n)
                           : Check.Fail("Espacement arceaux", $"{ActualHoopSpacing:0} mm > 1500 mm", n);
                yield return ActualBarSpacing < 300 ? Check.Ok("Espacement filants", $"{ActualBarSpacing:0} mm < 300", n)
                           : Check.Fail("Espacement filants", $"{ActualBarSpacing:0} mm ≥ 300 mm", n);
                yield return P.CageDiameter >= 650 && P.CageDiameter <= 800 ? Check.Ok("Diamètre crinoline", $"{P.CageDiameter:0} mm (≈ 700)", n)
                           : Check.Warn("Diamètre crinoline", $"{P.CageDiameter:0} mm : 700 mm usuel", n);
            }

            var rs = SectionProperties.Of(ProfileCatalog.Get(P.RungProfile));
            double mEd = SteelCheck.GammaQ * P.RungLoad * 1000 * P.Width / 4;
            yield return SteelCheck.Bending($"Résistance échelon ({P.RungLoad:0.0} kN au centre)", mEd, rs.WplMin, P.Grade);
            yield return Check.Info("Exécution", "Échelle fixe : moyen d'accès permanent, éléments de fixation à justifier selon le support (béton, charpente).");
        }
    }
}
