using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using MetalForge.Core.Geometry;
using MetalForge.Core.Model;
using MetalForge.Core.Profiles;

namespace MetalForge.Core.Gates
{
    [TypeConverter(typeof(Model.EnumDescriptionConverter))]
    public enum GateType
    {
        [Description("Battant 1 vantail")] Battant1,
        [Description("Battant 2 vantaux")] Battant2,
        [Description("Coulissant autoportant")] Coulissant,
        [Description("Panneau de clôture")] Cloture
    }

    /// <summary>Paramètres portail / portillon / clôture à barreaudage. Cotes en mm.</summary>
    public sealed class GateParameters
    {
        [Category("1. Ouverture"), DisplayName("Type")] public GateType Type { get; set; } = GateType.Battant2;
        [Category("1. Ouverture"), DisplayName("Largeur de passage (mm)"), Description("Entre piliers.")] public double ClearWidth { get; set; } = 3500;
        [Category("1. Ouverture"), DisplayName("Hauteur vantail (mm)")] public double Height { get; set; } = 1600;
        [Category("1. Ouverture"), DisplayName("Jeu au sol (mm)")] public double GroundGap { get; set; } = 50;
        [Category("1. Ouverture"), DisplayName("Jeu côté gonds (mm)")] public double HingeGap { get; set; } = 20;
        [Category("1. Ouverture"), DisplayName("Jeu entre vantaux (mm)")] public double MeetingGap { get; set; } = 10;
        [Category("1. Ouverture"), DisplayName("Queue coulissant (% passage)"), Description("Contrepoids autoportant : 40 % à 50 % conseillé.")]
        public double TailPercent { get; set; } = 45;

        [Category("2. Profilés"), DisplayName("Cadre")] public string FrameProfile { get; set; } = "TR60x40x2";
        [Category("2. Profilés"), DisplayName("Traverse intermédiaire")] public string RailProfile { get; set; } = "TR40x20x2";
        [Category("2. Profilés"), DisplayName("Nombre de traverses intermédiaires")] public int IntermediateRails { get; set; } = 1;
        [Category("2. Profilés"), DisplayName("Barreau")] public string BarProfile { get; set; } = "TC20x20x1.5";
        [Category("2. Profilés"), DisplayName("Vide max entre barreaux (mm)")] public double MaxOpening { get; set; } = 110;

        [Category("4. Quincaillerie & sécurité"), DisplayName("Motorisé"), Description("Active les contrôles NF EN 12453 / NF EN 12604.")]
        public bool Motorized { get; set; } = false;
        [Category("4. Quincaillerie & sécurité"), DisplayName("Entraxe des gonds (mm)"), Description("0 = hauteur du vantail - 200 mm.")]
        public double HingeSpacing { get; set; } = 0;

        [Category("3. Piliers"), DisplayName("Piliers acier")] public bool Posts { get; set; } = true;
        [Category("3. Piliers"), DisplayName("Profil pilier")] public string PostProfile { get; set; } = "TC100x100x4";
        [Category("3. Piliers"), DisplayName("Dépassement pilier (mm)")] public double PostAboveGate { get; set; } = 100;
        [Category("3. Piliers"), DisplayName("Scellement pilier (mm)")] public double PostEmbedment { get; set; } = 500;
    }

    public sealed class GateDesigner : IDesigner
    {
        public GateParameters P { get; }
        public GateDesigner(GateParameters p = null) { P = p ?? new GateParameters(); }
        public string Title => "Portail / clôture";
        public object Parameters => P;

        public DesignResult Compute()
        {
            if (P.ClearWidth <= 0 || P.Height <= 0) throw new ArgumentException("Largeur et hauteur doivent être positives.");
            var frame = ProfileCatalog.Get(P.FrameProfile);
            var post = ProfileCatalog.Get(P.PostProfile);
            var m = new StructureModel { Name = $"{Describe(P.Type)} {P.ClearWidth:0}x{P.Height:0}" };
            var r = new DesignResult { Model = m };

            // Vantaux : (x départ, largeur, a une queue ?)
            var leaves = new List<(double X, double W, double Tail)>();
            switch (P.Type)
            {
                case GateType.Battant1:
                    leaves.Add((P.HingeGap, P.ClearWidth - 2 * P.HingeGap, 0)); break;
                case GateType.Battant2:
                    double w = (P.ClearWidth - 2 * P.HingeGap - P.MeetingGap) / 2;
                    leaves.Add((P.HingeGap, w, 0));
                    leaves.Add((P.HingeGap + w + P.MeetingGap, w, 0)); break;
                case GateType.Coulissant:
                    double tail = P.ClearWidth * P.TailPercent / 100.0;
                    leaves.Add((P.HingeGap - tail, P.ClearWidth - P.HingeGap + tail, tail)); break; // la queue part derrière le pilier gauche
                case GateType.Cloture:
                    leaves.Add((P.HingeGap, P.ClearWidth - 2 * P.HingeGap, 0)); break;
            }

            int leafIndex = 0;
            foreach (var (x0, lw, tail) in leaves)
            {
                leafIndex++;
                int before = m.Members.Count;
                BuildLeaf(m, x0, lw, tail, frame, r);
                double mass = m.Members.Skip(before).Sum(x => x.Mass);
                r.Add($"Vantail {leafIndex} : largeur", $"{lw:0} mm");
                r.Add($"Vantail {leafIndex} : masse", $"{mass:0.0} kg");
                if (P.Type == GateType.Battant1 || P.Type == GateType.Battant2)
                {
                    // Gonds : le vantail en console crée un couple équilibré par les deux gonds (H = W·b / a)
                    double wN = mass * 9.81, a = P.HingeSpacing > 0 ? P.HingeSpacing : P.Height - 200;
                    double hR = wN * (lw / 2) / Math.Max(a, 100);
                    r.Add($"Vantail {leafIndex} : effort horizontal par gond", $"{hR / 1000:0.00} kN (gond haut tiré, gond bas poussé)");
                    r.Add($"Vantail {leafIndex} : effort vertical (gond bas)", $"{wN / 1000:0.00} kN");
                    if (lw > 2500) r.Checks.Add(Check.Warn("Largeur vantail", $"{lw:0} mm > 2500 : risque d'affaissement, prévoir écharpe ou roulette."));
                    if (mass > 200) r.Checks.Add(Check.Warn("Masse vantail", $"{mass:0} kg : vérifier gonds et motorisation."));
                }
            }

            if (P.Posts && P.Type != GateType.Coulissant)
            {
                foreach (var x in new[] { -post.VisibleWidth / 2, P.ClearWidth + post.VisibleWidth / 2 })
                    m.AddMember("PIL", "Pilier", P.PostProfile, new Vec3(x, -P.PostEmbedment, 0), new Vec3(x, P.GroundGap + P.Height + P.PostAboveGate, 0));
            }
            else if (P.Posts)
            {
                foreach (var x in new[] { -post.VisibleWidth / 2, P.ClearWidth + post.VisibleWidth / 2 })
                    m.AddMember("PIL", "Pilier", P.PostProfile, new Vec3(x, -P.PostEmbedment, -frame.WidthAcross - 30), new Vec3(x, P.GroundGap + P.Height + P.PostAboveGate, -frame.WidthAcross - 30));
            }

            if (P.Type == GateType.Coulissant)
            {
                r.Checks.Add(P.TailPercent >= 40 ? Check.Ok("Queue autoportant", $"{P.TailPercent:0} % du passage")
                                                 : Check.Warn("Queue autoportant", $"{P.TailPercent:0} % < 40 % : basculement possible"));
                r.Checks.Add(Check.Warn("Rail autoportant", "Rail et chariots à choisir chez le fabricant selon masse et portée."));
            }
            if (P.Motorized)
            {
                string reference = Standards.Norms.Portails;
                double[] gaps = P.Type == GateType.Battant2 ? new[] { P.HingeGap, P.MeetingGap } : new[] { P.HingeGap };
                foreach (var gp in gaps)
                    r.Checks.Add(gp >= 25 ? Check.Ok("Écrasement des doigts", $"jeu {gp:0} mm ≥ 25", Standards.Norms.Ecartements)
                                          : Check.Warn("Écrasement des doigts", $"jeu {gp:0} mm < 25 mm aux bords de fermeture : prévoir profil de sécurité / bord sensible", Standards.Norms.Ecartements));
                if (P.Type == GateType.Coulissant)
                    r.Checks.Add(Check.Warn("Cisaillement au pilier", "Barreaudage passant devant le pilier : prévoir une protection (grillage fin, tôle) dans la zone de passage ou une distance de sécurité.", reference));
                r.Checks.Add(Check.Info("Motorisation", "Efforts de fermeture, détection d'obstacle et notice selon NF EN 12453 ; marquage CE du portail motorisé (NF EN 13241).", reference));
            }
            else
                r.Checks.Add(Check.Info("Motorisation", "Portail manuel. En cas de motorisation ultérieure : NF EN 12453 (efforts, détection).", Standards.Norms.Portails));
            return r;
        }

        private void BuildLeaf(StructureModel m, double x0, double lw, double tail, Profile frame, DesignResult r)
        {
            double a = frame.VisibleWidth / 2;
            double y0 = P.GroundGap, y1 = P.GroundGap + P.Height;
            double x1 = x0 + lw;
            // Cadre en coupe d'onglet : axes à a du bord, longueur de débit = axe + largeur de profil
            var c1 = new Vec3(x0 + a, y0 + a, 0); var c2 = new Vec3(x1 - a, y0 + a, 0);
            var c3 = new Vec3(x1 - a, y1 - a, 0); var c4 = new Vec3(x0 + a, y1 - a, 0);
            foreach (var (s, e, role) in new[] { (c1, c2, "Cadre bas"), (c2, c3, "Montant"), (c3, c4, "Cadre haut"), (c4, c1, "Montant") })
                m.AddMember("CA", role, P.FrameProfile, s, e).CutAllowance = frame.VisibleWidth;

            double barStart = x0 + 2 * a, barEnd = x1 - 2 * a;
            if (tail > 0)
            {
                // Montant de séparation queue / partie barreaudée + écharpe de rigidité
                double xs = x0 + tail;
                m.AddMember("CA", "Montant queue", P.FrameProfile, new Vec3(xs, y0 + 2 * a, 0), new Vec3(xs, y1 - 2 * a, 0));
                m.AddMember("E", "Écharpe", P.RailProfile, new Vec3(x0 + 2 * a, y0 + 2 * a, 0), new Vec3(xs - a, y1 - 2 * a, 0));
                barStart = xs + a;
            }

            // Traverses intermédiaires équiréparties
            var rail = ProfileCatalog.Get(P.RailProfile);
            for (int k = 1; k <= P.IntermediateRails; k++)
            {
                double y = y0 + 2 * a + k * (P.Height - 4 * a) / (P.IntermediateRails + 1);
                m.AddMember("T", "Traverse", P.RailProfile, new Vec3(barStart, y, 0), new Vec3(barEnd, y, 0));
            }

            // Barreaux traversants (perçage des traverses) répartis à vide constant
            var bar = ProfileCatalog.Get(P.BarProfile);
            double clear = barEnd - barStart;
            int n = Math.Max(0, (int)Math.Ceiling((clear - P.MaxOpening) / (bar.VisibleWidth + P.MaxOpening) - 1e-9));
            double gap = (clear - n * bar.VisibleWidth) / (n + 1);
            for (int k = 1; k <= n; k++)
            {
                double x = barStart + k * gap + (k - 0.5) * bar.VisibleWidth;
                m.AddMember("B", "Barreau", P.BarProfile, new Vec3(x, y0 + 2 * a, 0), new Vec3(x, y1 - 2 * a, 0));
            }
            if (!r.Values.Any(v => v.Key == "Vide entre barreaux"))
            {
                r.Add("Barreaux par vantail", n.ToString());
                r.Add("Vide entre barreaux", $"{gap:0.0} mm");
                r.Checks.Add(gap <= 110 ? Check.Ok("Vide barreaux", $"{gap:0.0} mm ≤ 110")
                                        : Check.Warn("Vide barreaux", $"{gap:0.0} mm > 110 mm (passage d'enfant / animal)"));
                if (rail.SectionHeight < bar.VisibleWidth + 6)
                    r.Checks.Add(Check.Warn("Traverses", "Traverse trop étroite pour des barreaux traversants : souder les barreaux en applique."));
            }
        }

        public static string Describe(GateType t)
        {
            var f = typeof(GateType).GetField(t.ToString());
            var d = (DescriptionAttribute)Attribute.GetCustomAttribute(f, typeof(DescriptionAttribute));
            return d?.Description ?? t.ToString();
        }
    }
}
