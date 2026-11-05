using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using MetalForge.Core.Geometry;
using MetalForge.Core.Model;
using MetalForge.Core.Profiles;
using MetalForge.Core.Railings;
using MetalForge.Core.Standards;
using MetalForge.Core.Structural;

namespace MetalForge.Core.Stairs
{
    [TypeConverter(typeof(Model.EnumDescriptionConverter))]
    public enum StairUsage
    {
        [Description("Habitation (intérieur d'un logement)")] Habitation,
        [Description("Habitation collective (parties communes)")] HabitationCollective,
        [Description("ERP neuf (accessibilité)")] ERP,
        [Description("Industriel / accès machine (ISO 14122-3)")] Industriel
    }

    [TypeConverter(typeof(Model.EnumDescriptionConverter))]
    public enum TreadType
    {
        [Description("Tôle plane")] TolePlane,
        [Description("Tôle pliée (plis avant/arrière)")] TolePliee,
        [Description("Caillebotis (tableau fabricant)")] Caillebotis
    }

    [TypeConverter(typeof(Model.EnumDescriptionConverter))]
    public enum StairRailing
    {
        [Description("Aucun")] Aucun,
        [Description("Côté droit")] Droite,
        [Description("Côté gauche")] Gauche,
        [Description("Deux côtés")] DeuxCotes
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

        [Category("2. Marches"), DisplayName("Type de marche")]
        public TreadType Tread { get; set; } = TreadType.TolePliee;

        [Category("2. Marches"), DisplayName("Épaisseur tôle marche (mm)"), Description("Tôle larmée / tôle pliée.")]
        public double TreadThickness { get; set; } = 4;

        [Category("2. Marches"), DisplayName("Hauteur des plis (mm)"), Description("Tôle pliée : hauteur des retours avant et arrière.")]
        public double FoldHeight { get; set; } = 40;

        [Category("2. Marches"), DisplayName("Recouvrement (mm)"), Description("Débord du nez sur la marche inférieure (≥ 10 mm en industriel).")]
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

        [Category("4. Garde-corps & main courante"), DisplayName("Garde-corps rampant"), Description("Généré sur le limon (pose à l'anglaise).")]
        public StairRailing Railing { get; set; } = StairRailing.Droite;

        [Category("4. Garde-corps & main courante"), DisplayName("Hauteur main courante (mm)"), Description("Mesurée à l'aplomb du nez de marche.")]
        public double HandrailHeight { get; set; } = 900;

        [Category("4. Garde-corps & main courante"), DisplayName("Profil poteau")] public string RailingPostProfile { get; set; } = "TC40x40x4";
        [Category("4. Garde-corps & main courante"), DisplayName("Profil main courante")] public string HandrailProfile { get; set; } = "TRD42.4x2";
        [Category("4. Garde-corps & main courante"), DisplayName("Profil barreau")] public string BalusterProfile { get; set; } = "TC20x20x1.5";

        [Category("5. Résistance"), DisplayName("Nuance acier")] public SteelGrade Grade { get; set; } = SteelGrade.S235;

        [Category("5. Résistance"), DisplayName("Charge d'exploitation qk (kN/m²)"),
         Description("0 = valeur par défaut selon l'usage (2,5 logement / 4,0 ERP et industriel) — à confirmer avec NF P06-111-2.")]
        public double LiveLoad { get; set; } = 0;

        [Category("5. Résistance"), DisplayName("Charge ponctuelle Qk (kN)"), Description("Appliquée au milieu de la marche.")]
        public double PointLoad { get; set; } = 2.0;

        public double EffectiveLiveLoad => LiveLoad > 0 ? LiveLoad : (Usage == StairUsage.Habitation || Usage == StairUsage.HabitationCollective) ? 2.5 : 4.0;

        public StairParameters Clone() => (StairParameters)MemberwiseClone();
    }

    /// <summary>Géométrie calculée de l'escalier.</summary>
    public sealed class StairGeometry
    {
        public int Risers;
        public int Treads;
        public double Riser;
        public double Going;
        public double Run;
        public double AngleDeg;
        public double Blondel;
        public double TreadDepth;
        public double Headroom = double.NaN;
        public double StringerLength;
        public double Nosing;
        public double NoseX(int i) => (i - 1) * Going - Nosing;
        public double NoseY(int i) => i * Riser;
        /// <summary>Profondeur réelle de la marche i : la dernière s'arrête au nez du palier (pas de recouvrement).</summary>
        public double DepthOf(int i) => i == Treads ? Going : TreadDepth;
    }

    public sealed class StairDesigner : IDesigner
    {
        public StairParameters P { get; }
        public StairDesigner(StairParameters p = null) { P = p ?? new StairParameters(); }

        public string Title => "Escalier droit — 2 limons";
        public object Parameters => P;

        /// <summary>Hauteur de la marche (épaisseur visible) : tôle plane = épaisseur, tôle pliée = plis.</summary>
        public double TreadDepthVertical => P.Tread == TreadType.TolePlane ? P.TreadThickness : Math.Max(P.FoldHeight, P.TreadThickness);

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

            if (P.OpeningLength > 0)
            {
                double xEdge = g.Run - P.Nosing - P.OpeningLength;
                double noseLineY = g.Riser / g.Going * (xEdge + P.Nosing) + g.Riser;
                g.Headroom = (P.FloorToFloor - P.SlabThickness) - noseLineY;
                if (xEdge < -P.Nosing - g.Going) g.Headroom = double.PositiveInfinity;
            }
            return g;
        }

        public DesignResult Compute()
        {
            var geo = Solve();
            var r = new DesignResult { Model = BuildModel(geo, out var railing) };
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
            r.Checks.AddRange(VerifyStrength(geo, r));
            if (railing != null)
                foreach (var c in railing.Checks.Where(c => c.Level != CheckLevel.Ok || c.Rule.StartsWith("Résistance")))
                    r.Checks.Add(new Check { Level = c.Level, Rule = "Garde-corps rampant — " + c.Rule, Message = c.Message, Reference = c.Reference });
            r.Checks.Add(Check.Info("Exécution", "Escalier = ouvrage porteur : fabrication selon NF EN 1090-2, classe EXC2 par défaut (marquage CE si mis sur le marché).", Norms.Execution));
            return r;
        }

        public IEnumerable<Check> Verify(StairGeometry g)
        {
            const string blondel = "Loi de Blondel";
            bool ind = P.Usage == StairUsage.Industriel;
            double bMin = 600, bMax = ind ? 660 : 650;
            if (g.Blondel >= bMin && g.Blondel <= bMax) yield return Check.Ok(blondel, $"2h+g = {g.Blondel:0} mm ({bMin:0}–{bMax:0})", ind ? Norms.AccesMachines3 : null);
            else if (!ind && g.Blondel >= 580 && g.Blondel <= 660) yield return Check.Warn(blondel, $"2h+g = {g.Blondel:0} mm, hors plage de confort 600–650");
            else yield return Check.Fail(blondel, $"2h+g = {g.Blondel:0} mm hors {bMin:0}–{bMax:0} mm", ind ? Norms.AccesMachines3 : null);

            switch (P.Usage)
            {
                case StairUsage.Habitation:
                {
                    string lg = Norms.AccessibiliteLogement + ", art. 12";
                    yield return g.Riser <= 180 ? Check.Ok("Hauteur de marche", $"{g.Riser:0.0} mm ≤ 180", lg) : Check.Fail("Hauteur de marche", $"{g.Riser:0.0} mm > 180 mm", lg);
                    yield return g.Going >= 240 ? Check.Ok("Giron", $"{g.Going:0} mm ≥ 240", lg) : Check.Fail("Giron", $"{g.Going:0} mm < 240 mm", lg);
                    yield return P.Width >= 800 ? Check.Ok("Emmarchement", $"{P.Width:0} mm ≥ 800", lg) : Check.Fail("Emmarchement", $"{P.Width:0} mm < 800 mm", lg);
                    yield return g.AngleDeg <= 42 ? Check.Ok("Inclinaison", $"{g.AngleDeg:0.0}°") : Check.Warn("Inclinaison", $"{g.AngleDeg:0.0}° > 42° : escalier raide");
                    break;
                }
                case StairUsage.HabitationCollective:
                {
                    string lg = Norms.AccessibiliteLogement;
                    yield return g.Riser <= 170 ? Check.Ok("Hauteur de marche", $"{g.Riser:0.0} mm ≤ 170", lg) : Check.Fail("Hauteur de marche", $"{g.Riser:0.0} mm > 170 mm", lg);
                    yield return g.Going >= 280 ? Check.Ok("Giron", $"{g.Going:0} mm ≥ 280", lg) : Check.Fail("Giron", $"{g.Going:0} mm < 280 mm", lg);
                    yield return P.Railing != StairRailing.Aucun ? Check.Ok("Main courante", "présente", lg) : Check.Fail("Main courante", "obligatoire en parties communes", lg);
                    break;
                }
                case StairUsage.ERP:
                {
                    string acc = Norms.AccessibiliteErp;
                    yield return g.Riser <= 160 ? Check.Ok("Hauteur de marche", $"{g.Riser:0} mm ≤ 160", acc) : Check.Fail("Hauteur de marche", $"{g.Riser:0} mm > 160 mm", acc);
                    yield return g.Going >= 280 ? Check.Ok("Giron", $"{g.Going:0} mm ≥ 280", acc) : Check.Fail("Giron", $"{g.Going:0} mm < 280 mm", acc);
                    yield return P.Width >= 1200 ? Check.Ok("Largeur", $"{P.Width:0} mm ≥ 1200", acc) : Check.Fail("Largeur", $"{P.Width:0} mm < 1200 mm entre mains courantes", acc);
                    yield return P.Railing == StairRailing.DeuxCotes ? Check.Ok("Mains courantes", "des deux côtés", acc)
                               : Check.Fail("Mains courantes", "obligatoires des deux côtés en ERP neuf", acc);
                    yield return P.HandrailHeight >= 800 && P.HandrailHeight <= 1000 ? Check.Ok("Hauteur main courante", $"{P.HandrailHeight:0} mm (800–1000)", acc)
                               : Check.Fail("Hauteur main courante", $"{P.HandrailHeight:0} mm hors 800–1000 mm", acc);
                    yield return Check.Info("Prolongement", $"Prolonger les mains courantes horizontalement d'un giron ({g.Going:0} mm) en haut et en bas ; contremarches 1re et dernière marche et nez de marche contrastés.", acc);
                    break;
                }
                case StairUsage.Industriel:
                {
                    string iso = Norms.AccesMachines3;
                    yield return g.AngleDeg >= 30 && g.AngleDeg <= 38 ? Check.Ok("Inclinaison", $"{g.AngleDeg:0.0}° (30–38°)", iso)
                               : Check.Fail("Inclinaison", $"{g.AngleDeg:0.0}° hors 30–38° (au-delà : échelle à marches)", iso);
                    yield return P.Nosing >= 10 ? Check.Ok("Recouvrement", $"{P.Nosing:0} mm ≥ 10", iso) : Check.Fail("Recouvrement", $"{P.Nosing:0} mm < 10 mm", iso);
                    yield return P.Width >= 600 ? Check.Ok("Largeur", $"{P.Width:0} mm") : Check.Warn("Largeur", $"{P.Width:0} mm < 600 mm (valeur usuelle, à confirmer)", iso);
                    break;
                }
            }

            if (!double.IsNaN(g.Headroom) && !double.IsInfinity(g.Headroom))
                yield return g.Headroom >= 2000 ? Check.Ok("Échappée", $"{g.Headroom:0} mm ≥ 2000")
                           : g.Headroom >= 1900 ? Check.Warn("Échappée", $"{g.Headroom:0} mm : tolérable, 2000 mm conseillé")
                           : Check.Fail("Échappée", $"{g.Headroom:0} mm < 1900 mm : allonger la trémie");

            if (P.Railing == StairRailing.Aucun)
                yield return Check.Warn("Garde-corps", "Aucun garde-corps : obligatoire côté vide (rampant ≥ 900 mm à l'aplomb du nez).", Norms.GardeCorps);
            else if (P.Usage != StairUsage.Industriel)
                yield return P.HandrailHeight >= 900 ? Check.Ok("Hauteur rampant", $"{P.HandrailHeight:0} mm ≥ 900 à l'aplomb du nez", Norms.GardeCorps)
                           : Check.Fail("Hauteur rampant", $"{P.HandrailHeight:0} mm < 900 mm", Norms.GardeCorps);
        }

        /// <summary>Limons : poutre bi-appuyée en projection horizontale. Marches : poutre sur deux appuis (emmarchement).</summary>
        private IEnumerable<Check> VerifyStrength(StairGeometry g, DesignResult r)
        {
            double qk = P.EffectiveLiveLoad * 1e-3;     // N/mm²
            var o = StringerOutline(g);
            double lh = o.TopEnd.X - o.BottomStart.X;    // portée horizontale
            double ls = lh / Math.Cos(g.AngleDeg * Math.PI / 180);
            var sp = ProfileCatalog.Get(P.StringerProfile);
            var ss = SectionProperties.Of(sp);
            double gk = r.Model.TotalMass * 9.81 / 2 / lh;           // N/mm horizontal, par limon
            double qkl = qk * P.Width / 2;                           // N/mm horizontal, par limon
            double pEd = SteelCheck.GammaG * gk + SteelCheck.GammaQ * qkl;
            double mEd = pEd * lh * lh / 8;
            double c2 = Math.Pow(Math.Cos(g.AngleDeg * Math.PI / 180), 2);
            double delta = 5 * (gk + qkl) * c2 * Math.Pow(ls, 4) / (384 * SteelCheck.E * ss.Iy);
            r.Add("Charge d'exploitation", $"{P.EffectiveLiveLoad:0.0} kN/m² + {P.PointLoad:0.0} kN ponctuel");
            r.Add("Moment ELU par limon", $"{mEd / 1e6:0.00} kN·m");
            r.Add("Flèche limon (ELS)", $"{delta:0.0} mm");
            yield return Check.Info("Hypothèses de charge", $"qk = {P.EffectiveLiveLoad:0.0} kN/m², Qk = {P.PointLoad:0.0} kN, poids propre {r.Model.TotalMass:0} kg ; γG = 1,35, γQ = 1,5", "NF EN 1991-1-1 / NF P06-111-2 (à confirmer)");
            yield return SteelCheck.Bending("Résistance limon", mEd, ss.Wy, P.Grade);
            yield return SteelCheck.Deflection("Flèche limon", delta, ls, 300, "critère usuel L/300");

            if (P.Tread == TreadType.Caillebotis)
            {
                yield return Check.Info("Marches caillebotis", "Vérifier la portée admissible dans le tableau du fabricant (charge, maille, hauteur de porteur).");
                yield break;
            }
            double d = g.TreadDepth, t = P.TreadThickness, span = P.Width;
            var ts = P.Tread == TreadType.TolePlane
                ? SectionProperties.Composite((0, 0, d, t))
                : SectionProperties.Composite((0, 0, d, t), (0, t, t, P.FoldHeight - t), (d - t, t, t, P.FoldHeight - t));
            double mU = SteelCheck.GammaQ * qk * d * span * span / 8;
            double mQ = SteelCheck.GammaQ * P.PointLoad * 1000 * span / 4;
            double dQ = P.PointLoad * 1000 * Math.Pow(span, 3) / (48 * SteelCheck.E * ts.Iy);
            r.Add("Flèche marche sous Qk", $"{dQ:0.0} mm");
            yield return SteelCheck.Bending("Résistance marche", Math.Max(mU, mQ), ts.Wy, P.Grade);
            var dc = SteelCheck.Deflection("Flèche marche (Qk au centre)", dQ, span, 250, "critère usuel L/250");
            if (dc.Level == CheckLevel.NonConforme && P.Tread == TreadType.TolePlane)
                dc.Message += " — passer en tôle pliée ou ajouter un raidisseur";
            yield return dc;
        }

        // ---------------------------------------------------------------------------------------
        // Modèle 3D. Repère : X = sens de montée, Y = vertical, Z = emmarchement (0 au milieu).
        // ---------------------------------------------------------------------------------------

        public StringerOutline StringerOutline(StairGeometry g)
        {
            var prof = ProfileCatalog.Get(P.StringerProfile);
            double tan = g.Riser / g.Going;
            double cos = Math.Cos(Math.Atan(tan));
            double xA = -g.Going - P.Nosing;
            double top = P.StringerAboveNosing;
            double bottom = top - prof.SectionHeight / cos;
            double xEnd = g.Run - P.Nosing;

            Func<double, double, double> xAtY = (y, off) => y / tan + xA - off / tan;
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

        private StructureModel BuildModel(StairGeometry g, out DesignResult railingResult)
        {
            var m = new StructureModel { Name = $"Escalier droit {g.Treads} marches — H{P.FloorToFloor:0}" };
            var sp = ProfileCatalog.Get(P.StringerProfile);
            var outline = StringerOutline(g);
            double zLim = P.Width / 2 + sp.WidthAcross / 2;

            foreach (var side in new[] { -1, 1 })
            {
                if (sp.Family == ProfileFamily.Plat)
                    m.AddPlate("LIM", "Limon tôle", new[] { outline.BottomStart, outline.BottomEnd, outline.TopEnd, outline.TopStart },
                               sp.T, side * zLim, sp.T);
                else
                {
                    double off = outline.AxisOffset;
                    double x0 = (0 - off) / outline.Tan + outline.XA;
                    var a = new Vec3(x0, 0, side * zLim);
                    var b = new Vec3(outline.TopEnd.X, outline.Tan * (outline.TopEnd.X - outline.XA) + off, side * zLim);
                    m.AddMember("LIM", "Limon", P.StringerProfile, a, b);
                }
            }
            g.StringerLength = (outline.TopEnd - outline.TopStart).Length;

            var ap = P.TreadAngles ? ProfileCatalog.Get(P.TreadAngleProfile) : null;
            double t = P.TreadThickness, hv = TreadDepthVertical;
            for (int i = 1; i <= g.Treads; i++)
            {
                double x1 = g.NoseX(i), x2 = x1 + g.DepthOf(i), yTop = g.NoseY(i);
                IEnumerable<Vec3> poly = P.Tread == TreadType.TolePliee
                    ? new[] { new Vec3(x1, yTop, 0), new Vec3(x2, yTop, 0), new Vec3(x2, yTop - hv, 0), new Vec3(x2 - t, yTop - hv, 0),
                              new Vec3(x2 - t, yTop - t, 0), new Vec3(x1 + t, yTop - t, 0), new Vec3(x1 + t, yTop - hv, 0), new Vec3(x1, yTop - hv, 0) }
                    : new[] { new Vec3(x1, yTop - hv, 0), new Vec3(x2, yTop - hv, 0), new Vec3(x2, yTop, 0), new Vec3(x1, yTop, 0) };
                m.AddPlate("M", "Marche", poly, P.Width - 4, 0, t);
                if (ap != null)
                {
                    double yUnder = P.Tread == TreadType.TolePliee ? yTop - t : yTop - hv;
                    double y = yUnder - ap.H / 2, z = P.Width / 2 - ap.B / 2;
                    foreach (var side in new[] { -1, 1 })
                        m.AddMember("C", "Cornière support", P.TreadAngleProfile, new Vec3(x1 + 10, y, side * z), new Vec3(x2 - 10, y, side * z));
                }
            }

            railingResult = null;
            if (P.Railing != StairRailing.Aucun)
            {
                var post = ProfileCatalog.Get(P.RailingPostProfile);
                var rd = new RailingDesigner(new RailingParameters
                {
                    Use = P.Usage == StairUsage.Industriel ? RailingUse.Industriel : P.Usage == StairUsage.ERP ? RailingUse.Public : RailingUse.Prive,
                    Slope = RailingSlope.Rampant, SlopeDeg = g.AngleDeg, Length = g.Run, Height = P.HandrailHeight,
                    Mounting = RailingMounting.Anglaise, SideMountDrop = Math.Min(150, sp.SectionHeight * 0.6), BasePlates = false,
                    PostProfile = P.RailingPostProfile, HandrailProfile = P.HandrailProfile, BottomRailProfile = "TR40x20x2",
                    InfillProfile = P.BalusterProfile,
                    Infill = P.Usage == StairUsage.Industriel ? RailingInfill.Industriel : RailingInfill.BarreauxVerticaux
                });
                railingResult = rd.Compute();
                double zr = P.Width / 2 + sp.WidthAcross + post.WidthAcross / 2;
                var sides = P.Railing == StairRailing.DeuxCotes ? new[] { -1, 1 } : P.Railing == StairRailing.Droite ? new[] { 1 } : new[] { -1 };
                foreach (var s in sides)
                    m.Merge(railingResult.Model, new Vec3(g.NoseX(1), g.NoseY(1), s * zr));
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
