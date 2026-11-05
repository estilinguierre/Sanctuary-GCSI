using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MetalForge.Core.Model;
using MetalForge.Core.Profiles;
using MetalForge.Core.Standards;

namespace MetalForge.Core.Fabrication
{
    /// <summary>Trous d'évent / de vidange d'un élément creux avant galvanisation à chaud.</summary>
    public sealed class VentHoleAdvice
    {
        public string ProfileId;
        public string Marks;
        public int Members;
        public double EquivalentDiameter;  // périmètre / π
        public int SingleHole;             // 1 trou par extrémité
        public int TwoHoles;               // ou 2 trous
        public int FourHoles;              // ou 4 trous
        public override string ToString() =>
            $"{ProfileId} ({Members} pièce(s) : {Marks}) → à chaque extrémité : 1 x Ø{SingleHole} ou 2 x Ø{TwoHoles} ou 4 x Ø{FourHoles}";
    }

    /// <summary>
    /// Règle de dimensionnement d'après NF EN ISO 14713-2 (tableaux annexe A, via aide Coatinc) :
    /// ouverture ≈ 25 % du diamètre équivalent de la section ; minimum 10 mm (pratique des galvaniseurs).
    /// Exemple de contrôle : 60x40 → 1 x Ø16, 2 x Ø12 ou 4 x Ø10.
    /// </summary>
    public static class GalvanizingAdvisor
    {
        public const double MinHole = 10;

        public static bool IsHollow(Profile p) =>
            p.Family == ProfileFamily.TubeCarre || p.Family == ProfileFamily.TubeRectangulaire || p.Family == ProfileFamily.TubeRond;

        public static VentHoleAdvice ForProfile(Profile p)
        {
            double deq = p.Family == ProfileFamily.TubeRond ? p.B : 2 * (p.B + p.H) / Math.PI;
            int Drill(double d) => (int)Math.Max(MinHole, Math.Ceiling(d / 2) * 2); // forets pairs
            double d1 = 0.25 * deq;
            return new VentHoleAdvice
            {
                ProfileId = p.Id, EquivalentDiameter = deq,
                SingleHole = Drill(d1), TwoHoles = Drill(d1 / Math.Sqrt(2)), FourHoles = Drill(d1 / 2)
            };
        }

        public static List<VentHoleAdvice> Advise(StructureModel model) =>
            model.Members.Where(m => IsHollow(m.Profile))
                 .GroupBy(m => m.ProfileId, StringComparer.OrdinalIgnoreCase)
                 .Select(g =>
                 {
                     var a = ForProfile(g.First().Profile);
                     a.Members = g.Count();
                     a.Marks = string.Join(",", g.Select(x => x.Mark).Take(8)) + (g.Count() > 8 ? "…" : "");
                     return a;
                 })
                 .OrderByDescending(a => a.EquivalentDiameter).ToList();

        public static string ToReport(StructureModel model)
        {
            var list = Advise(model);
            var sb = new StringBuilder();
            sb.AppendLine("=== GALVANISATION À CHAUD — TROUS D'ÉVENT ===");
            sb.AppendLine($"Référence : {Norms.Galvanisation} (à confirmer avec le galvaniseur)");
            if (list.Count == 0) { sb.AppendLine("Aucun profil creux : pas d'évent nécessaire."); return sb.ToString(); }
            foreach (var a in list) sb.AppendLine("• " + a);
            sb.AppendLine();
            sb.AppendLine("Règles :");
            sb.AppendLine("• Trous diamétralement opposés, au plus près des extrémités et des assemblages, débouchant dans les éléments soudés adjacents.");
            sb.AppendLine("• Aucun volume creux fermé (risque d'explosion au bain) ; percer aussi les platines et bouchons.");
            sb.AppendLine("• Recouvrements soudés : souder en continu ou laisser un jeu ≥ 2 mm pour l'écoulement du zinc.");
            return sb.ToString();
        }
    }
}
