using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using MetalForge.Core.Geometry;
using MetalForge.Core.Model;
using MetalForge.Core.Profiles;

namespace MetalForge.Core.Stairs
{
    [TypeConverter(typeof(Model.EnumDescriptionConverter))]
    public enum StairUsage
    {
        [Description("Habitation")] Habitation,
        [Description("ERP (accessibilité)")] ERP,
        [Description("Industriel / accès machine")] Industriel
    }

    /// <summary>Paramètres d'un escalier droit métallique à deux limons. Cotes en mm.</summary>
    public sealed class StairParameters
    {
        [Category("1. Trémie & usage"), DisplayName("Usage"), Description("Fixe les règles de vérification appliquées.")]
        public StairUsage Usage { get; set; } = StairUsage.Habitation;

        [Category("1. Trémie & usage"), DisplayName("Hauteur à monter (mm)"), Description("Hauteur de sol fini à sol fini.")]
        public double FloorToFloor { get; set; } = 2700;

        [Category("1. Trémie & usage"), DisplayName("Reculement imposé (mm)"),
         Description("Distance horizontale du nez de la 1re marche au bord de trémie. 0 = calculé par la loi de Blondel.")]
        public double ImposedRun { get; set; } = 0;

        [Category("1. Trémie & usage"), DisplayName("Épaisseur plancher (mm)"), Description("Pour le calcul de l'échappée.")]
        public double SlabThickness { get; set; } = 200;

        [Category("1. Trémie & usage"), DisplayName("Longueur trémie (mm)"), Description("0 = échappée non vérifiée.")]
        public double OpeningLength { get; set; } = 3400;

        [Category("2. Marches"), DisplayName("Emmarchement (mm)"), Description("Largeur libre entre limons.")]
        public double Width { get; set; } = 900;

        [Category("2. Marches"), DisplayName("Hauteur de marche visée (mm)")]
        public double TargetRiser { get; set; } = 175;

        [Category("2. Marches"), DisplayName("Module de Blondel (mm)"), Description("2h + g visé, 600 à 650 (630 conseillé).")]
        public double BlondelModule { get; set; } = 630;

        [Category("2. Marches"), DisplayName("Épaisseur tôle marche (mm)"), Description("Tôle larmée / caillebotis équivalent.")]
        public double TreadThickness { get; set; } = 6;

        [Category("2. Marches"), DisplayName("Recouvrement (mm)"), Description("Débord du nez sur la marche inférieure.")]
        public double Nosing { get; set; } = 30;

        [Category("3. Limons & supports"), DisplayName("Profil limon"),
         Description("Ex. PL250x12 (limon tôle, découpe laser), UPN160, TR150x100x5.")]
        public string StringerProfile { get; set; } = "PL250x12";

        [Category("3. Limons & supports"), DisplayName("Débord limon au-dessus du nez (mm)")]
        public double StringerAboveNosing { get; set; } = 40;

        [Category("3. Limons & supports"), DisplayName("Cornières support de marche")]
        public bool TreadAngles { get; set; } = true;

        [Category("3. Limons & supports"), DisplayName("Profil cornière support")]
        public string TreadAngleProfile { get; set; } = "L40x40x4";

        public StairParameters Clone() => (StairParameters)MemberwiseClone();
    }

    /// <summary>Géométrie calculée de l'escalier.</summary>
    public sealed class StairGeometry
    {
        public int Risers;            // nombre de hauteurs
        public int Treads;            // nombre de marches (arrivée = plancher)
        public double Riser;          // h
        public double Going;          // g (giron)
        public double Run;            // reculement nez 1re marche → bord trémie
        public double AngleDeg;
        public double Blondel;        // 2h + g
        public double TreadDepth;     // giron + recouvrement
        public double Headroom = double.NaN; // échappée
        public double StringerLength;
        /// <summary>Abscisse du nez de la marche i (1..Treads).</summary>
        public double NoseX(int i) => (i - 1) * Going - Nosing;
        public double NoseY(int i) => i * Riser;
        /// <summary>Profondeur réelle de la marche i : la dernière s'arrête au nez du palier (pas de recouvrement).</summary>
        public double DepthOf(int i) => i == Treads ? Going : TreadDepth;
        public double Nosing;
    }

    public sealed class StairDesigner : IDesigner
    {
        public StairParameters P { get; }
        public StairDesigner(StairParameters p = null) { P = p ?? new StairParameters(); }

        public string Title => "Escalier droit — 2 limons";
        public object Parameters => P;

        public StairGeometry Solve()
        {
            if (P.FloorToFloor <= 0) throw new ArgumentException("La hauteur à monter doit être positive.");
            if (P.TargetRiser <= 0) throw new ArgumentException("La hauteur de marche visée doit être positive.");

            var g = new StairGeometry { Nosing = P.Nosing };
            g.Risers = Math.Max(2, (int)Math.Round(P.FloorToFloor / P.TargetRiser));
            g.Treads = g.Risers - 1;
            g.Riser = P.FloorToFloor / g.Risers;
            g.Going = P.ImposedRun > 0 ? P.ImposedRun / g.Treads : P.BlondelModule - 2 * g.Riser;
            if (g.Going <= 0) throw new ArgumentException("Giron nul : augmenter le reculement ou réduire la hauteur de marche.");
            g.Run = g.Treads * g.Going;
            g.Blondel = 2 * g.Riser + g.Going;
            g.AngleDeg = Math.Atan2(g.Riser, g.Going) * 180 / Math.PI;
            g.TreadDepth = g.Going + P.Nosing;

            // Échappée : hauteur libre au droit du chevêtre opposé, mesurée à la verticale du nez de marche.
            if (P.OpeningLength > 0)
            {
                double xEdge = g.Run - P.Nosing - P.OpeningLength;   // bord de trémie côté départ
                double noseLineY = g.Riser / g.Going * (xEdge + P.Nosing) + g.Riser;
                g.Headroom = (P.FloorToFloor - P.SlabThickness) - noseLineY;
                if (xEdge < -P.Nosing - g.Going) g.Headroom = double.PositiveInfinity; // trémie plus longue que l'escalier
            }
            return g;
        }

        public DesignResult Compute()
        {
            var geo = Solve();
            var r = new DesignResult { Model = BuildModel(geo) };
            r.Add("Nombre de hauteurs", geo.Risers.ToString());
            r.Add("Nombre de marches", geo.Treads.ToString());
            r.Add("Hauteur de marche h", $"{geo.Riser:0.0} mm");
            r.Add("Giron g", $"{geo.Going:0.0} mm");
            r.Add("Module 2h + g", $"{geo.Blondel:0.0} mm");
            r.Add("Profondeur marche", $"{geo.TreadDepth:0.0} mm");
            r.Add("Reculement", $"{geo.Run:0} mm");
            r.Add("Inclinaison", $"{geo.AngleDeg:0.0}°");
            r.Add("Longueur limon", $"{geo.StringerLength:0} mm");
            if (!double.IsNaN(geo.Headroom))
                r.Add("Échappée", double.IsInfinity(geo.Headroom) ? "dégagée" : $"{geo.Headroom:0} mm");
            r.Checks.AddRange(Verify(geo));
            return r;
        }

        public IEnumerable<Check> Verify(StairGeometry g)
        {
            const string blondel = "Loi de Blondel";
            if (g.Blondel >= 600 && g.Blondel <= 650) yield return Check.Ok(blondel, $"2h+g = {g.Blondel:0} mm (600–650)");
            else if (g.Blondel >= 580 && g.Blondel <= 660) yield return Check.Warn(blondel, $"2h+g = {g.Blondel:0} mm, hors plage de confort 600–650");
            else yield return Check.Fail(blondel, $"2h+g = {g.Blondel:0} mm, escalier inconfortable");

            switch (P.Usage)
            {
                case StairUsage.Habitation:
                    yield return Range("Hauteur de marche", g.Riser, 150, 200, 210, "mm", "DTU / usage courant");
                    yield return g.Going >= 240 ? Check.Ok("Giron", $"{g.Going:0} mm ≥ 240")
                               : g.Going >= 210 ? Check.Warn("Giron", $"{g.Going:0} mm : acceptable, 240 mm conseillé")
                               : Check.Fail("Giron", $"{g.Going:0} mm < 210 mm");
                    yield return P.Width >= 800 ? Check.Ok("Emmarchement", $"{P.Width:0} mm")
                               : Check.Warn("Emmarchement", $"{P.Width:0} mm < 800 mm conseillé");
                    yield return g.AngleDeg <= 42 ? Check.Ok("Inclinaison", $"{g.AngleDeg:0.0}°")
                               : Check.Warn("Inclinaison", $"{g.AngleDeg:0.0}° > 42° : escalier raide");
                    break;
                case StairUsage.ERP:
                    const string acc = "Arrêté du 20/04/2017 (accessibilité ERP)";
                    yield return g.Riser <= 160 ? Check.Ok("Hauteur de marche", $"{g.Riser:0} mm ≤ 160", acc)
                               : Check.Fail("Hauteur de marche", $"{g.Riser:0} mm > 160 mm", acc);
                    yield return g.Going >= 280 ? Check.Ok("Giron", $"{g.Going:0} mm ≥ 280", acc)
                               : Check.Fail("Giron", $"{g.Going:0} mm < 280 mm", acc);
                    yield return P.Width >= 1200 ? Check.Ok("Largeur", $"{P.Width:0} mm ≥ 1200", acc)
                               : Check.Fail("Largeur", $"{P.Width:0} mm < 1200 mm entre mains courantes", acc);
                    break;
                case StairUsage.Industriel:
                    const string nfe = "NF E 85-015 (valeurs à confirmer selon édition)";
                    yield return g.AngleDeg >= 30 && g.AngleDeg <= 45 ? Check.Ok("Inclinaison", $"{g.AngleDeg:0.0}° (30–45°)", nfe)
                               : Check.Fail("Inclinaison", $"{g.AngleDeg:0.0}° hors 30–45°", nfe);
                    yield return P.Width >= 600 ? Check.Ok("Largeur", $"{P.Width:0} mm ≥ 600", nfe)
                               : Check.Fail("Largeur", $"{P.Width:0} mm < 600 mm", nfe);
                    yield return g.Riser <= 250 ? Check.Ok("Hauteur de marche", $"{g.Riser:0} mm", nfe)
                               : Check.Fail("Hauteur de marche", $"{g.Riser:0} mm > 250 mm", nfe);
                    break;
            }

            if (!double.IsNaN(g.Headroom) && !double.IsInfinity(g.Headroom))
                yield return g.Headroom >= 2000 ? Check.Ok("Échappée", $"{g.Headroom:0} mm ≥ 2000")
                           : g.Headroom >= 1900 ? Check.Warn("Échappée", $"{g.Headroom:0} mm : tolérable, 2000 mm conseillé")
                           : Check.Fail("Échappée", $"{g.Headroom:0} mm < 1900 mm : allonger la trémie");

            if (g.Risers >= 4)
                yield return Check.Warn("Garde-corps / main courante", "Prévoir main courante et garde-corps côté vide (module Garde-corps, mode rampant).", "NF P01-012");
        }

        private static Check Range(string rule, double v, double min, double max, double hardMax, string unit, string reference)
        {
            if (v >= min && v <= max) return Check.Ok(rule, $"{v:0.0} {unit}", reference);
            if (v <= hardMax && v >= min - 10) return Check.Warn(rule, $"{v:0.0} {unit} (conseillé {min}–{max})", reference);
            return Check.Fail(rule, $"{v:0.0} {unit} hors limites", reference);
        }

        // ---------------------------------------------------------------------------------------
        // Modèle 3D. Repère : X = sens de montée, Y = vertical, Z = emmarchement (0 au milieu).
        // ---------------------------------------------------------------------------------------

        public StringerOutline StringerOutline(StairGeometry g)
        {
            var prof = ProfileCatalog.Get(P.StringerProfile);
            double tan = g.Riser / g.Going;
            double cos = Math.Cos(Math.Atan(tan));
            double xA = -g.Going - P.Nosing;               // ligne de nez : y = tan * (x - xA)
            double top = P.StringerAboveNosing;              // décalage vertical rive haute / ligne de nez
            double bottom = top - prof.SectionHeight / cos;  // décalage vertical rive basse
            double xEnd = g.Run - P.Nosing;                  // about haut contre le chevêtre

            Func<double, double, double> xAtY = (y, off) => y / tan + xA - off / tan; // x tel que tan(x-xA)+off = y
            Func<double, double, double> yAtX = (x, off) => tan * (x - xA) + off;

            var o = new StringerOutline
            {
                TopStart = new Vec3(xAtY(0, top), 0, 0),
                BottomStart = new Vec3(xAtY(0, bottom), 0, 0),
                BottomEnd = new Vec3(xEnd, yAtX(xEnd, bottom), 0),
                TopEnd = new Vec3(xEnd, yAtX(xEnd, top), 0),
                AxisOffset = (top + bottom) / 2,
                Tan = tan,
                XA = xA
            };
            for (int i = 1; i <= g.Treads; i++)
                o.TreadMarks.Add((new Vec3(g.NoseX(i), g.NoseY(i), 0), new Vec3(g.NoseX(i) + g.DepthOf(i), g.NoseY(i), 0)));
            return o;
        }

        private StructureModel BuildModel(StairGeometry g)
        {
            var m = new StructureModel { Name = $"Escalier droit {g.Treads} marches — H{P.FloorToFloor:0}" };
            var sp = ProfileCatalog.Get(P.StringerProfile);
            var outline = StringerOutline(g);
            double zLim = P.Width / 2 + sp.WidthAcross / 2;

            foreach (var side in new[] { -1, 1 })
            {
                if (sp.Family == ProfileFamily.Plat)
                {
                    // Limon tôle découpé : contour exact (coupes d'aplomb), à débiter au laser.
                    m.AddPlate("LIM", "Limon tôle", new[] { outline.BottomStart, outline.BottomEnd, outline.TopEnd, outline.TopStart },
                               sp.T, side * zLim, sp.T);
                }
                else
                {
                    // Limon profilé : membre suivant l'axe, coupes d'about à reprendre (Ajuster/Prolonger).
                    double off = outline.AxisOffset;
                    double x0 = (0 - off) / outline.Tan + outline.XA;
                    var a = new Vec3(x0, 0, side * zLim);
                    var b = new Vec3(outline.TopEnd.X, outline.Tan * (outline.TopEnd.X - outline.XA) + off, side * zLim);
                    m.AddMember("LIM", "Limon", P.StringerProfile, a, b);
                }
            }
            g.StringerLength = (outline.TopEnd - outline.TopStart).Length;

            var ap = P.TreadAngles ? ProfileCatalog.Get(P.TreadAngleProfile) : null;
            for (int i = 1; i <= g.Treads; i++)
            {
                double x1 = g.NoseX(i), x2 = x1 + g.DepthOf(i), yTop = g.NoseY(i), yBot = yTop - P.TreadThickness;
                m.AddPlate("M", "Marche", new[] { new Vec3(x1, yBot, 0), new Vec3(x2, yBot, 0), new Vec3(x2, yTop, 0), new Vec3(x1, yTop, 0) },
                           P.Width - 4, 0, P.TreadThickness);
                if (ap != null)
                {
                    double y = yBot - ap.H / 2, z = P.Width / 2 - ap.B / 2;
                    foreach (var side in new[] { -1, 1 })
                        m.AddMember("C", "Cornière support", P.TreadAngleProfile, new Vec3(x1 + 10, y, side * z), new Vec3(x2 - 10, y, side * z));
                }
            }
            return m;
        }
    }

    /// <summary>Contour d'un limon (vue de face) et repères de traçage des marches.</summary>
    public sealed class StringerOutline
    {
        public Vec3 TopStart, BottomStart, BottomEnd, TopEnd;
        public double AxisOffset, Tan, XA;
        public List<(Vec3 Front, Vec3 Back)> TreadMarks { get; } = new List<(Vec3, Vec3)>();
        public IEnumerable<Vec3> Polygon => new[] { BottomStart, BottomEnd, TopEnd, TopStart };
        public double DevelopedLength => new[] { BottomStart, BottomEnd, TopEnd, TopStart }.Max(p => p.X) -
                                         new[] { BottomStart, BottomEnd, TopEnd, TopStart }.Min(p => p.X);
    }
}
