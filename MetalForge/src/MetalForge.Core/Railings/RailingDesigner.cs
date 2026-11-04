using System;
using System.Collections.Generic;
using System.ComponentModel;
using MetalForge.Core.Geometry;
using MetalForge.Core.Model;
using MetalForge.Core.Profiles;

namespace MetalForge.Core.Railings
{
    [TypeConverter(typeof(Model.EnumDescriptionConverter))]
    public enum RailingSlope { [Description("Droit (palier, balcon)")] Droit, [Description("Rampant (escalier)")] Rampant }
    [TypeConverter(typeof(Model.EnumDescriptionConverter))]
    public enum RailingMounting { [Description("À la française (sur dalle)")] Francaise, [Description("À l'anglaise (nez de dalle)")] Anglaise }
    [TypeConverter(typeof(Model.EnumDescriptionConverter))]
    public enum RailingInfill { [Description("Barreaudage vertical")] BarreauxVerticaux, [Description("Lisses horizontales")] LissesHorizontales }

    /// <summary>Paramètres d'un garde-corps métallique. Cotes en mm.</summary>
    public sealed class RailingParameters
    {
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

        [Category("2. Dimensions"), DisplayName("Hauteur (mm)"), Description("Du sol fini (ou ligne de nez) au-dessus de la main courante.")]
        public double Height { get; set; } = 1000;

        [Category("2. Dimensions"), DisplayName("Entraxe max poteaux (mm)")]
        public double MaxPostSpacing { get; set; } = 1500;

        [Category("2. Dimensions"), DisplayName("Jeu sous lisse basse (mm)")]
        public double BottomGap { get; set; } = 60;

        [Category("2. Dimensions"), DisplayName("Vide max remplissage (mm)"), Description("110 mm selon NF P01-012.")]
        public double MaxOpening { get; set; } = 110;

        [Category("3. Profilés"), DisplayName("Poteau")] public string PostProfile { get; set; } = "TC40x40x3";
        [Category("3. Profilés"), DisplayName("Main courante")] public string HandrailProfile { get; set; } = "TR40x20x2";
        [Category("3. Profilés"), DisplayName("Lisse basse")] public string BottomRailProfile { get; set; } = "TR40x20x2";
        [Category("3. Profilés"), DisplayName("Barreau / lisse")] public string InfillProfile { get; set; } = "TC20x20x1.5";

        [Category("3. Profilés"), DisplayName("Remplissage")]
        public RailingInfill Infill { get; set; } = RailingInfill.BarreauxVerticaux;

        [Category("3. Profilés"), DisplayName("Platines de fixation"), Description("Ajoute une platine par poteau (pose à la française).")]
        public bool BasePlates { get; set; } = true;

        [Category("3. Profilés"), DisplayName("Platine (L x l x ép.)")] public string BasePlateSize { get; set; } = "100x100x8";
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

            double tan = P.Slope == RailingSlope.Rampant ? Math.Tan(P.SlopeDeg * Math.PI / 180) : 0;
            double cos = Math.Cos(Math.Atan(tan));
            Func<double, double> ground = x => x * tan; // ligne de nez (rampant) ou sol (droit)

            Spans = Math.Max(1, (int)Math.Ceiling((P.Length - post.VisibleWidth) / P.MaxPostSpacing - 1e-9));
            PostSpacing = (P.Length - post.VisibleWidth) / Spans;
            double clear = PostSpacing - post.VisibleWidth;

            var m = new StructureModel { Name = $"Garde-corps {(P.Slope == RailingSlope.Rampant ? "rampant" : "droit")} L{P.Length:0} H{P.Height:0}" };

            // Hauteurs (verticales, mesurées depuis la ligne de sol / nez)
            double hrAxis = P.Height - hr.SectionHeight / 2 / cos;
            double postTop = P.Height - hr.SectionHeight / cos;
            double lbAxis = P.BottomGap + lb.SectionHeight / 2 / cos;
            double infBottom = P.BottomGap + lb.SectionHeight / cos;
            double postBottom = P.Mounting == RailingMounting.Anglaise ? -P.SideMountDrop : (P.BasePlates ? PlateT() : 0);

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

            // Main courante continue sur toute la longueur
            m.AddMember("MC", "Main courante", P.HandrailProfile, new Vec3(0, ground(0) + hrAxis, 0), new Vec3(P.Length, ground(P.Length) + hrAxis, 0));

            int perSpan = 0; double opening = 0;
            for (int s = 0; s < Spans; s++)
            {
                double xa = postX[s] + post.VisibleWidth / 2, xb = postX[s + 1] - post.VisibleWidth / 2;
                m.AddMember("LB", "Lisse basse", P.BottomRailProfile, new Vec3(xa, ground(xa) + lbAxis, 0), new Vec3(xb, ground(xb) + lbAxis, 0));

                if (P.Infill == RailingInfill.BarreauxVerticaux)
                {
                    // n barreaux tels que le vide (clear - n·b)/(n+1) ≤ vide max
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
                    double clearH = (postTop - infBottom) * cos; // hauteur perpendiculaire disponible
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
            r.Add(P.Infill == RailingInfill.BarreauxVerticaux ? "Barreaux par travée" : "Lisses par travée", perSpan.ToString());
            r.Add("Vide réel", $"{opening:0.0} mm");
            r.Checks.AddRange(Verify(opening));
            return r;
        }

        private IEnumerable<Check> Verify(double opening)
        {
            const string nf = "NF P01-012";
            double hMin = P.Slope == RailingSlope.Rampant ? 900 : 1000;
            yield return P.Height >= hMin ? Check.Ok("Hauteur de protection", $"{P.Height:0} mm ≥ {hMin:0}", nf)
                       : Check.Fail("Hauteur de protection", $"{P.Height:0} mm < {hMin:0} mm", nf);
            yield return opening <= P.MaxOpening + 1e-6 && P.MaxOpening <= 110
                       ? Check.Ok("Vide entre éléments", $"{opening:0.0} mm ≤ 110", nf)
                       : Check.Fail("Vide entre éléments", $"{opening:0.0} mm > 110 mm", nf);
            yield return P.BottomGap <= 110 ? Check.Ok("Jeu en partie basse", $"{P.BottomGap:0} mm ≤ 110", nf)
                       : Check.Fail("Jeu en partie basse", $"{P.BottomGap:0} mm > 110 mm", nf);
            if (P.Infill == RailingInfill.LissesHorizontales)
                yield return Check.Warn("Escaladabilité", "Remplissage horizontal escaladable : vérifier la hauteur de protection au-dessus des points d'appui (zone 0–450 mm).", nf);
            yield return PostSpacing <= 1500 ? Check.Ok("Entraxe poteaux", $"{PostSpacing:0} mm")
                       : Check.Warn("Entraxe poteaux", $"{PostSpacing:0} mm > 1500 : vérifier la résistance à la poussée (NF P06-111-2).");
        }

        private (double L, double W, double T) PlateDims()
        {
            var parts = (P.BasePlateSize ?? "100x100x8").ToLowerInvariant().Split('x');
            double D(int i, double def) => parts.Length > i && double.TryParse(parts[i], System.Globalization.NumberStyles.Float,
                                                     System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : def;
            return (D(0, 100), D(1, 100), D(2, 8));
        }

        private double PlateT() => PlateDims().T;
    }
}
