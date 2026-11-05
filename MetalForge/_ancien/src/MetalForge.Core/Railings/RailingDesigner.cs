using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using MetalForge.Core.Geometry;
using MetalForge.Core.Model;
using MetalForge.Core.Profiles;
using MetalForge.Core.Standards;
using MetalForge.Core.Structural;

namespace MetalForge.Core.Railings
{
    [TypeConverter(typeof(Model.EnumDescriptionConverter))]
    public enum RailingSlope { [Description("Droit (palier, balcon)")] Droit, [Description("Rampant (escalier)")] Rampant }
    [TypeConverter(typeof(Model.EnumDescriptionConverter))]
    public enum RailingMounting { [Description("À la française (sur dalle)")] Francaise, [Description("À l'anglaise (nez de dalle)")] Anglaise }
    [TypeConverter(typeof(Model.EnumDescriptionConverter))]
    public enum RailingInfill
    {
        [Description("Barreaudage vertical")] BarreauxVerticaux,
        [Description("Lisses horizontales")] LissesHorizontales,
        [Description("Industriel : lisse + sous-lisse + plinthe")] Industriel
    }
    [TypeConverter(typeof(Model.EnumDescriptionConverter))]
    public enum RailingUse
    {
        [Description("Logement / privé (cat. A-B)")] Prive,
        [Description("Public / ERP (cat. C-D)")] Public,
        [Description("Industriel (NF EN ISO 14122-3)")] Industriel
    }

    /// <summary>Paramètres d'un garde-corps métallique. Cotes en mm.</summary>
    public sealed class RailingParameters
    {
        [Category("1. Implantation"), DisplayName("Usage"), Description("Fixe les règles et la poussée horizontale appliquées.")]
        public RailingUse Use { get; set; } = RailingUse.Prive;

        [Category("1. Implantation"), DisplayName("Type"), Description("Droit ou rampant d'escalier.")]
        public RailingSlope Slope { get; set; } = RailingSlope.Droit;

        [Category("1. Implantation"), DisplayName("Longueur (mm)"), Description("Longueur hors tout (projection horizontale si rampant).")]
        public double Length { get; set; } = 3000;

        [Category("1. Implantation"), DisplayName("Pente rampant (°)"), Description("Inclinaison de l'escalier (module Escalier).")]
        public double SlopeDeg { get; set; } = 33;

        [Category("1. Implantation"), DisplayName("Pose")]
        public RailingMounting Mounting { get; set; } = RailingMounting.Francaise;

        [Category("1. Implantation"), DisplayName("Descente à l'anglaise (mm)"), Description("Longueur du poteau sous le niveau fini.")]
        public double SideMountDrop { get; set; } = 200;

        [Category("1. Implantation"), DisplayName("Épaisseur du dispositif (mm)"),
         Description("Profondeur entre le nu intérieur et le vide. ≥ 500 mm : hauteur réduite à 800 mm admise (NF P01-012:2024).")]
        public double Thickness { get; set; } = 0;

        [Category("2. Dimensions"), DisplayName("Hauteur (mm)"), Description("Du sol fini (ou ligne de nez) au-dessus de la main courante.")]
        public double Height { get; set; } = 1000;

        [Category("2. Dimensions"), DisplayName("Entraxe max poteaux (mm)")]
        public double MaxPostSpacing { get; set; } = 1500;

        [Category("2. Dimensions"), DisplayName("Jeu sous lisse basse (mm)")]
        public double BottomGap { get; set; } = 60;

        [Category("2. Dimensions"), DisplayName("Vide max remplissage (mm)"), Description("Gabarit T1 = 110 mm (NF P01-012) ; 500 mm en industriel.")]
        public double MaxOpening { get; set; } = 110;

        [Category("3. Profilés"), DisplayName("Poteau")] public string PostProfile { get; set; } = "TC40x40x4";
        [Category("3. Profilés"), DisplayName("Main courante")] public string HandrailProfile { get; set; } = "TR40x20x2";
        [Category("3. Profilés"), DisplayName("Lisse basse")] public string BottomRailProfile { get; set; } = "TR40x20x2";
        [Category("3. Profilés"), DisplayName("Barreau / lisse")] public string InfillProfile { get; set; } = "TC20x20x1.5";
        [Category("3. Profilés"), DisplayName("Plinthe (industriel)")] public string ToeBoardProfile { get; set; } = "PL100x5";

        [Category("3. Profilés"), DisplayName("Remplissage")]
        public RailingInfill Infill { get; set; } = RailingInfill.BarreauxVerticaux;

        [Category("3. Profilés"), DisplayName("Platines de fixation"), Description("Ajoute une platine par poteau (pose à la française).")]
        public bool BasePlates { get; set; } = true;

        [Category("3. Profilés"), DisplayName("Platine (L x l x ép.)")] public string BasePlateSize { get; set; } = "100x100x8";

        [Category("4. Résistance"), DisplayName("Nuance acier")] public SteelGrade Grade { get; set; } = SteelGrade.S235;

        [Category("4. Résistance"), DisplayName("Poussée horizontale (kN/m)"),
         Description("0 = valeur par défaut selon l'usage : 0,6 (privé) / 1,0 (public, industriel). NF P06-111-2/A1.")]
        public double LineLoad { get; set; } = 0;

        public double EffectiveLineLoad => LineLoad > 0 ? LineLoad : Use == RailingUse.Prive ? 0.6 : 1.0;
    }

    public sealed class RailingDesigner : IDesigner
    {
        public RailingParameters P { get; }
        public RailingDesigner(RailingParameters p = null) { P = p ?? new RailingParameters(); }
        public string Title => "Garde-corps";
        public object Parameters => P;

        public int Spans { get; private set; }
        public double PostSpacing { get; private set; }
        public int InfillPerSpan { get; private set; }
        public double ActualOpening { get; private set; }

        public DesignResult Compute()
        {
            if (P.Length <= 0 || P.Height <= 0) throw new ArgumentException("Longueur et hauteur doivent être positives.");
            var post = ProfileCatalog.Get(P.PostProfile);
            var hr = ProfileCatalog.Get(P.HandrailProfile);
            var lb = ProfileCatalog.Get(P.BottomRailProfile);
            var inf = ProfileCatalog.Get(P.InfillProfile);
            bool industrial = P.Infill == RailingInfill.Industriel;

            double tan = P.Slope == RailingSlope.Rampant ? Math.Tan(P.SlopeDeg * Math.PI / 180) : 0;
            double cos = Math.Cos(Math.Atan(tan));
            Func<double, double> ground = x => x * tan;

            Spans = Math.Max(1, (int)Math.Ceiling((P.Length - post.VisibleWidth) / P.MaxPostSpacing - 1e-9));
            PostSpacing = (P.Length - post.VisibleWidth) / Spans;
            double clear = PostSpacing - post.VisibleWidth;

            var m = new StructureModel { Name = $"Garde-corps {(P.Slope == RailingSlope.Rampant ? "rampant" : "droit")} L{P.Length:0} H{P.Height:0}" };

            double hrAxis = P.Height - hr.SectionHeight / 2 / cos;
            double postTop = P.Height - hr.SectionHeight / cos;
            double lbAxis = P.BottomGap + lb.SectionHeight / 2 / cos;
            double infBottom = P.BottomGap + lb.SectionHeight / cos;
            double postBottom = P.Mounting == RailingMounting.Anglaise ? -P.SideMountDrop : (P.BasePlates ? PlateDims().T : 0);

            var postX = new List<double>();
            for (int i = 0; i <= Spans; i++) postX.Add(post.VisibleWidth / 2 + i * PostSpacing);

            foreach (var x in postX)
            {
                m.AddMember("P", "Poteau", P.PostProfile, new Vec3(x, ground(x) + postBottom, 0), new Vec3(x, ground(x) + postTop, 0));
                if (P.BasePlates && P.Mounting == RailingMounting.Francaise)
                {
                    var (pl, pw, pt) = PlateDims();
                    double y0 = ground(x);
                    m.AddPlate("PL", "Platine", new[] { new Vec3(x - pl / 2, y0, 0), new Vec3(x + pl / 2, y0, 0), new Vec3(x + pl / 2, y0 + pt, 0), new Vec3(x - pl / 2, y0 + pt, 0) },
                               pw, 0, pt);
                }
            }

            m.AddMember("MC", "Main courante", P.HandrailProfile, new Vec3(0, ground(0) + hrAxis, 0), new Vec3(P.Length, ground(P.Length) + hrAxis, 0));

            int perSpan = 0; double opening = 0;
            for (int s = 0; s < Spans; s++)
            {
                double xa = postX[s] + post.VisibleWidth / 2, xb = postX[s + 1] - post.VisibleWidth / 2;
                if (industrial)
                {
                    // Plinthe ≥ 100 mm posée à 10 mm du sol, sous-lisse à mi-hauteur entre plinthe et lisse
                    var tb = ProfileCatalog.Get(P.ToeBoardProfile);
                    double tbAxis = 10 + tb.SectionHeight / 2 / cos;
                    m.AddMember("PLI", "Plinthe", P.ToeBoardProfile, new Vec3(xa, ground(xa) + tbAxis, 0), new Vec3(xb, ground(xb) + tbAxis, 0));
                    double tbTop = 10 + tb.SectionHeight / cos;
                    double klAxis = (tbTop + postTop) / 2;
                    m.AddMember("SL", "Sous-lisse", P.BottomRailProfile, new Vec3(xa, ground(xa) + klAxis, 0), new Vec3(xb, ground(xb) + klAxis, 0));
                    perSpan = 1;
                    opening = Math.Max(postTop - (klAxis + lb.SectionHeight / 2 / cos), klAxis - lb.SectionHeight / 2 / cos - tbTop) * cos;
                    continue;
                }

                m.AddMember("LB", "Lisse basse", P.BottomRailProfile, new Vec3(xa, ground(xa) + lbAxis, 0), new Vec3(xb, ground(xb) + lbAxis, 0));
                if (P.Infill == RailingInfill.BarreauxVerticaux)
                {
                    perSpan = Math.Max(0, (int)Math.Ceiling((clear - P.MaxOpening) / (inf.VisibleWidth + P.MaxOpening) - 1e-9));
                    opening = (clear - perSpan * inf.VisibleWidth) / (perSpan + 1);
                    for (int k = 1; k <= perSpan; k++)
                    {
                        double x = xa + k * opening + (k - 0.5) * inf.VisibleWidth;
                        m.AddMember("B", "Barreau", P.InfillProfile, new Vec3(x, ground(x) + infBottom, 0), new Vec3(x, ground(x) + postTop, 0));
                    }
                }
                else
                {
                    double clearH = (postTop - infBottom) * cos;
                    double ih = inf.SectionHeight;
                    perSpan = Math.Max(0, (int)Math.Ceiling((clearH - P.MaxOpening) / (ih + P.MaxOpening) - 1e-9));
                    opening = (clearH - perSpan * ih) / (perSpan + 1);
                    for (int k = 1; k <= perSpan; k++)
                    {
                        double off = infBottom + (k * opening + (k - 0.5) * ih) / cos;
                        m.AddMember("LI", "Lisse intermédiaire", P.InfillProfile, new Vec3(xa, ground(xa) + off, 0), new Vec3(xb, ground(xb) + off, 0));
                    }
                }
            }
            InfillPerSpan = perSpan;
            ActualOpening = opening;

            var r = new DesignResult { Model = m };
            r.Add("Nombre de travées", Spans.ToString());
            r.Add("Entraxe poteaux", $"{PostSpacing:0.0} mm");
            r.Add("Nombre de poteaux", (Spans + 1).ToString());
            r.Add(industrial ? "Sous-lisses par travée" : P.Infill == RailingInfill.BarreauxVerticaux ? "Barreaux par travée" : "Lisses par travée", perSpan.ToString());
            r.Add("Vide réel", $"{opening:0.0} mm");
            r.Checks.AddRange(Verify(opening, m));
            r.Checks.AddRange(VerifyStrength(r));
            r.Checks.Add(Check.Info("Exécution", "Garde-corps non porteur : NF EN 1090 non exigée selon la FFB, sauf prescription du marché (souvent EXC2 demandée).", Norms.Execution));
            return r;
        }

        private IEnumerable<Check> Verify(double opening, StructureModel m)
        {
            string nf = Norms.GardeCorps;
            if (P.Use == RailingUse.Industriel || P.Infill == RailingInfill.Industriel)
            {
                string iso = Norms.AccesMachines3;
                yield return P.Height >= 1000 && P.Height <= 1100 ? Check.Ok("Hauteur lisse supérieure", $"{P.Height:0} mm (1000–1100)", iso)
                           : Check.Fail("Hauteur lisse supérieure", $"{P.Height:0} mm hors 1000–1100 mm", iso);
                if (P.Infill == RailingInfill.Industriel)
                {
                    var tb = ProfileCatalog.Get(P.ToeBoardProfile);
                    yield return tb.SectionHeight >= 100 ? Check.Ok("Plinthe", $"{tb.SectionHeight:0} mm ≥ 100, posée à 10 mm du sol", iso)
                               : Check.Fail("Plinthe", $"{tb.SectionHeight:0} mm < 100 mm", iso);
                    yield return opening <= 500 ? Check.Ok("Vides lisse / sous-lisse / plinthe", $"{opening:0} mm ≤ 500", iso)
                               : Check.Fail("Vides lisse / sous-lisse / plinthe", $"{opening:0} mm > 500 mm : ajouter une sous-lisse", iso);
                }
                else
                    yield return opening <= 500 ? Check.Ok("Vide entre éléments", $"{opening:0} mm ≤ 500", iso)
                               : Check.Fail("Vide entre éléments", $"{opening:0} mm > 500 mm", iso);
                if (P.Use != RailingUse.Industriel)
                    yield return Check.Fail("Usage", "Remplissage industriel (sous-lisse) non admis en logement / ERP : vides > 110 mm.", nf);
            }
            else
            {
                double hMin = P.Slope == RailingSlope.Rampant ? 900 : P.Thickness >= 500 ? 800 : 1000;
                string why = P.Slope == RailingSlope.Rampant ? " (rampant, à l'aplomb du nez)" : P.Thickness >= 500 ? " (épaisseur ≥ 500 mm)" : "";
                yield return P.Height >= hMin ? Check.Ok("Hauteur de protection", $"{P.Height:0} mm ≥ {hMin:0}{why}", nf)
                           : Check.Fail("Hauteur de protection", $"{P.Height:0} mm < {hMin:0} mm{why}", nf);
                yield return opening <= Math.Min(P.MaxOpening, 110) + 1e-6
                           ? Check.Ok("Gabarit T1 (Ø 110)", $"vide {opening:0.0} mm ≤ 110", nf)
                           : Check.Fail("Gabarit T1 (Ø 110)", $"vide {opening:0.0} mm > 110 mm", nf);
                yield return P.BottomGap <= 110 ? Check.Ok("Vide en partie basse", $"{P.BottomGap:0} mm ≤ 110", nf)
                           : Check.Fail("Vide en partie basse", $"{P.BottomGap:0} mm > 110 mm", nf);

                // Zone basse 0–600 mm : aucun élément ne doit servir de point d'appui (révision 2024 : 450 → 600 mm)
                if (P.Infill == RailingInfill.LissesHorizontales)
                {
                    int climbable = 0;
                    foreach (var mem in m.Members)
                        if (mem.Role == "Lisse intermédiaire" && mem.Start.Y - mem.Start.X * (P.Slope == RailingSlope.Rampant ? Math.Tan(P.SlopeDeg * Math.PI / 180) : 0) <= 600)
                            climbable++;
                    yield return climbable > 0
                        ? Check.Fail("Zone basse non escaladable (0–600 mm)", $"{climbable} lisse(s) horizontale(s) dans la zone basse : points d'appui interdits. Utiliser un barreaudage vertical ou une tôle pleine en partie basse.", nf)
                        : Check.Ok("Zone basse non escaladable (0–600 mm)", "aucune lisse dans la zone basse", nf);
                }
                else
                    yield return Check.Ok("Zone basse non escaladable (0–600 mm)", "barreaudage vertical : pas de point d'appui", nf);
            }
            string refSpacing = P.Use == RailingUse.Industriel ? Norms.AccesMachines3 : nf;
            yield return PostSpacing <= 1500 ? Check.Ok("Entraxe poteaux", $"{PostSpacing:0} mm ≤ 1500", refSpacing)
                       : Check.Warn("Entraxe poteaux", $"{PostSpacing:0} mm > 1500 : justifier par le calcul");
        }

        /// <summary>
        /// Poussée q (kN/m) à hauteur H : poteau en console (M = 1,5·q·e·H), main courante continue (M ≈ 1,5·q·e²/10),
        /// effort d'arrachement des chevilles de platine (2 chevilles tendues, bras de levier = L - 2·30 mm).
        /// </summary>
        private IEnumerable<Check> VerifyStrength(DesignResult r)
        {
            double q = P.EffectiveLineLoad;                 // kN/m = N/mm
            double e = PostSpacing, h = P.Height;
            var post = SectionProperties.Of(ProfileCatalog.Get(P.PostProfile));
            var hr = SectionProperties.Of(ProfileCatalog.Get(P.HandrailProfile));
            double mPost = SteelCheck.GammaQ * q * e * h;   // N·mm (poussée perpendiculaire au plan : axe faible du poteau dans le plan)
            double mHr = SteelCheck.GammaQ * q * e * e / 10;
            string load = Norms.ChargesGardeCorps;
            r.Add("Poussée horizontale", $"{q.ToString("0.0#", CultureInfo.InvariantCulture)} kN/m à {h:0} mm");
            r.Add("Moment ELU pied de poteau", $"{mPost / 1e6:0.00} kN·m");
            yield return Check.Info("Charge appliquée", $"{q:0.0#} kN/m ({(P.LineLoad > 0 ? "imposée" : P.Use == RailingUse.Prive ? "catégorie A-B" : "catégorie C-D")}), γQ = 1,5", load);
            yield return SteelCheck.Bending("Résistance poteau (console)", mPost, post.WplMin, P.Grade);
            yield return SteelCheck.Bending("Résistance main courante", mHr, hr.WplMin, P.Grade);

            // Flèche en tête de poteau sous charge de service (indicative : pas de critère normatif chiffré ici)
            double d = q * e * Math.Pow(h, 3) / (3 * SteelCheck.E * post.Imin);
            r.Add("Flèche tête de poteau (ELS)", $"{d:0.0} mm");
            yield return d <= h / 50 ? Check.Ok("Déformation poteau", $"{d:0.0} mm (indicatif ≤ H/50)")
                       : Check.Warn("Déformation poteau", $"{d:0.0} mm > H/50 : garde-corps souple, renforcer le poteau");

            if (P.BasePlates && P.Mounting == RailingMounting.Francaise)
            {
                var (_, pw, _) = PlateDims();
                double lever = Math.Max(20, pw - 30);  // chevilles à ~30 mm du bord, pivot sur le bord comprimé
                double nAnchor = mPost / lever / 2 / 1000;  // kN par cheville (2 chevilles tendues)
                r.Add("Traction par cheville (ELU)", $"{nAnchor:0.00} kN");
                yield return Check.Info("Chevilles de platine", $"≈ {nAnchor:0.00} kN de traction par cheville (2 tendues, bras {lever:0} mm) : à valider avec le fabricant (ETE, distances aux bords).");
            }
        }

        private (double L, double W, double T) PlateDims()
        {
            var parts = (P.BasePlateSize ?? "100x100x8").ToLowerInvariant().Split('x');
            double D(int i, double def) => parts.Length > i && double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : def;
            return (D(0, 100), D(1, 100), D(2, 8));
        }
    }
}
