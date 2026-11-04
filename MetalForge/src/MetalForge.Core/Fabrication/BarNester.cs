using System;
using System.Collections.Generic;
using System.Linq;
using MetalForge.Core.Model;
using MetalForge.Core.Profiles;

namespace MetalForge.Core.Fabrication
{
    /// <summary>Ligne de débit : n pièces identiques d'un même profilé.</summary>
    public sealed class CutPiece
    {
        public string Mark { get; set; }
        public string Role { get; set; }
        public string ProfileId { get; set; }
        public double Length { get; set; }
        public int Quantity { get; set; } = 1;

        /// <summary>Regroupe les barres d'un modèle en lignes de débit (même profil, même longueur à 0,5 mm près).</summary>
        public static List<CutPiece> FromModel(StructureModel model) =>
            model.Members
                 .GroupBy(m => (Profile: m.ProfileId.ToUpperInvariant(), Len: Math.Round(m.CutLength * 2) / 2))
                 .Select(g => new CutPiece
                 {
                     Mark = string.Join(",", g.Select(x => x.Mark)),
                     Role = string.Join("/", g.Select(x => x.Role).Distinct()),
                     ProfileId = g.First().ProfileId,
                     Length = g.Key.Len,
                     Quantity = g.Count()
                 })
                 .OrderBy(c => c.ProfileId).ThenByDescending(c => c.Length)
                 .ToList();
    }

    public sealed class NestingSettings
    {
        public double StockLength { get; set; } = 6000;
        /// <summary>Épaisseur de lame / trait de scie (mm).</summary>
        public double Kerf { get; set; } = 3;
        /// <summary>Chute d'équerrage en début de barre (mm).</summary>
        public double EndTrim { get; set; } = 10;
        /// <summary>Longueur mini d'une chute réutilisable (mm).</summary>
        public double MinReusableOffcut { get; set; } = 500;
        /// <summary>Longueurs commerciales spécifiques par profilé (sinon StockLength).</summary>
        public Dictionary<string, double> StockLengthByProfile { get; } = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Chutes déjà en stock, utilisées en priorité.</summary>
        public List<(string ProfileId, double Length)> ExistingOffcuts { get; } = new List<(string, double)>();

        public double StockFor(string profileId) =>
            StockLengthByProfile.TryGetValue(profileId, out var l) ? l : StockLength;
    }

    public sealed class Cut
    {
        public string Mark;
        public double Length;
        public double Offset; // position du début de coupe sur la barre
    }

    public sealed class StockBar
    {
        public string ProfileId;
        public double StockLength;
        public bool FromOffcut;
        public List<Cut> Cuts { get; } = new List<Cut>();
        public double Used;      // longueur consommée (trim + pièces + traits)
        public double Remaining => StockLength - Used;
        public double PiecesLength => Cuts.Sum(c => c.Length);
    }

    public sealed class ProfileNesting
    {
        public string ProfileId;
        public List<StockBar> Bars { get; } = new List<StockBar>();
        public List<CutPiece> Oversize { get; } = new List<CutPiece>();
        public int NewBars => Bars.Count(b => !b.FromOffcut);
        public double PurchasedLength => Bars.Where(b => !b.FromOffcut).Sum(b => b.StockLength);
        public double Yield => Bars.Sum(b => b.StockLength) > 0 ? Bars.Sum(b => b.PiecesLength) / Bars.Sum(b => b.StockLength) : 0;
    }

    public sealed class NestingResult
    {
        public List<ProfileNesting> Profiles { get; } = new List<ProfileNesting>();
        public NestingSettings Settings;

        public double PurchasedMass =>
            Profiles.Sum(p => ProfileCatalog.TryGet(p.ProfileId, out var pr) ? pr.MassPerMeter * p.PurchasedLength / 1000 : 0);

        public IEnumerable<(string ProfileId, double Length)> ReusableOffcuts =>
            Profiles.SelectMany(p => p.Bars).Where(b => b.Remaining >= Settings.MinReusableOffcut).Select(b => (b.ProfileId, b.Remaining));

        public string ToReport()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== PLAN DE DÉBIT ===");
            sb.AppendLine($"Trait de scie {Settings.Kerf} mm — équerrage {Settings.EndTrim} mm — chute réutilisable ≥ {Settings.MinReusableOffcut} mm");
            foreach (var p in Profiles)
            {
                sb.AppendLine();
                sb.AppendLine($"■ {p.ProfileId} : {p.NewBars} barre(s) neuve(s) de {Settings.StockFor(p.ProfileId):0} mm, rendement {p.Yield:P1}");
                int i = 0;
                foreach (var b in p.Bars)
                {
                    i++;
                    var cuts = string.Join(" | ", b.Cuts.Select(c => $"{c.Mark} {c.Length:0.#}"));
                    sb.AppendLine($"   Barre {i}{(b.FromOffcut ? " (chute stock)" : "")} [{b.StockLength:0}] : {cuts}  → reste {b.Remaining:0}");
                }
                foreach (var o in p.Oversize)
                    sb.AppendLine($"   ⚠ {o.Mark} {o.Length:0} mm x{o.Quantity} > longueur commerciale : prévoir aboutage.");
            }
            sb.AppendLine();
            sb.AppendLine($"Masse matière achetée : {PurchasedMass:0.0} kg");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Optimisation de débit 1D (barres) : heuristique « Best Fit Decreasing ».
    /// Garantie classique : au plus 11/9·OPT + 6/9 barres ; en pratique proche de l'optimum pour la serrurerie.
    /// </summary>
    public static class BarNester
    {
        public static NestingResult Nest(IEnumerable<CutPiece> pieces, NestingSettings settings = null)
        {
            settings = settings ?? new NestingSettings();
            var result = new NestingResult { Settings = settings };

            foreach (var grp in pieces.Where(p => p.Quantity > 0 && p.Length > 0).GroupBy(p => p.ProfileId, StringComparer.OrdinalIgnoreCase))
            {
                var pn = new ProfileNesting { ProfileId = grp.Key };
                double stock = settings.StockFor(grp.Key);

                foreach (var (pid, len) in settings.ExistingOffcuts.Where(o => string.Equals(o.ProfileId, grp.Key, StringComparison.OrdinalIgnoreCase)))
                    pn.Bars.Add(new StockBar { ProfileId = grp.Key, StockLength = len, FromOffcut = true, Used = settings.EndTrim });

                // Une unité par pièce physique, avec son repère individuel si disponible (ex. "B1,B2,B3").
                var units = grp.SelectMany(p =>
                               {
                                   var marks = (p.Mark ?? "?").Split(',');
                                   return Enumerable.Range(0, p.Quantity).Select(i => new CutPiece
                                   {
                                       Mark = marks.Length == p.Quantity ? marks[i] : p.Mark,
                                       Role = p.Role, ProfileId = p.ProfileId, Length = p.Length, Quantity = 1
                                   });
                               })
                               .OrderByDescending(p => p.Length).ToList();

                foreach (var piece in units)
                {
                    if (piece.Length + settings.EndTrim > stock)
                    {
                        var ov = pn.Oversize.FirstOrDefault(o => o.Mark == piece.Mark);
                        if (ov == null) pn.Oversize.Add(new CutPiece { Mark = piece.Mark, ProfileId = piece.ProfileId, Length = piece.Length, Quantity = 1, Role = piece.Role });
                        else ov.Quantity++;
                        continue;
                    }
                    StockBar best = null; double bestRest = double.MaxValue;
                    foreach (var b in pn.Bars)
                    {
                        double need = piece.Length + (b.Cuts.Count > 0 ? settings.Kerf : 0);
                        double rest = b.Remaining - need;
                        if (rest >= -1e-9 && rest < bestRest) { best = b; bestRest = rest; }
                    }
                    if (best == null)
                    {
                        best = new StockBar { ProfileId = grp.Key, StockLength = stock, Used = settings.EndTrim };
                        pn.Bars.Add(best);
                    }
                    if (best.Cuts.Count > 0) best.Used += settings.Kerf;
                    best.Cuts.Add(new Cut { Mark = piece.Mark, Length = piece.Length, Offset = best.Used });
                    best.Used += piece.Length;
                }
                pn.Bars.RemoveAll(b => b.FromOffcut && b.Cuts.Count == 0);
                result.Profiles.Add(pn);
            }
            return result;
        }
    }
}
