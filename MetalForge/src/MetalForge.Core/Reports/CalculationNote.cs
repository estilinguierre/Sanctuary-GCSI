using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using MetalForge.Core.Costing;
using MetalForge.Core.Export;
using MetalForge.Core.Fabrication;
using MetalForge.Core.Model;
using MetalForge.Core.Profiles;
using MetalForge.Core.Standards;

namespace MetalForge.Core.Reports
{
    /// <summary>
    /// Note de calcul / dossier de fabrication autonome (un seul fichier HTML, imprimable en PDF depuis le navigateur).
    /// </summary>
    public static class CalculationNote
    {
        public static string Build(string designerTitle, DesignResult r, NestingResult nest = null, CostEstimate cost = null, bool galvanized = false,
                                   string project = null, DateTime? date = null)
        {
            var fr = new CultureInfo("fr-FR");
            string H(string s) => WebUtility.HtmlEncode(s ?? "");
            var sb = new StringBuilder();
            sb.Append("<!doctype html><html lang=\"fr\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
            sb.Append($"<title>{H(r.Model.Name)} — note de calcul</title><style>{Css}</style></head><body>");
            sb.Append("<header><div><h1>").Append(H(r.Model.Name)).Append("</h1><p class=\"sub\">").Append(H(designerTitle))
              .Append(" — note de calcul et dossier de fabrication</p></div><div class=\"meta\">")
              .Append(project == null ? "" : $"<div>Affaire : <b>{H(project)}</b></div>")
              .Append($"<div>Date : {(date ?? DateTime.Now).ToString("d MMMM yyyy", fr)}</div><div>MetalForge pour SolidWorks</div></div></header>");

            int fails = r.Checks.Count(c => c.Level == CheckLevel.NonConforme), warns = r.Checks.Count(c => c.Level == CheckLevel.Avertissement);
            sb.Append($"<div class=\"verdict {(fails > 0 ? "ko" : warns > 0 ? "warn" : "ok")}\">")
              .Append(fails > 0 ? $"✖ {fails} non-conformité(s)" : warns > 0 ? $"⚠ Conforme avec {warns} point(s) d'attention" : "✔ Toutes les vérifications sont satisfaites")
              .Append($" — masse {r.Model.TotalMass.ToString("0.0", fr)} kg, surface à traiter {r.Model.TotalPaintArea.ToString("0.00", fr)} m²</div>");

            sb.Append("<section><h2>1. Vue de face</h2><div class=\"fig\">").Append(SvgDrawings.FrontView(r.Model)).Append("</div></section>");

            sb.Append("<section><h2>2. Résultats</h2><table class=\"kv\">");
            foreach (var kv in r.Values) sb.Append($"<tr><th>{H(kv.Key)}</th><td>{H(kv.Value)}</td></tr>");
            sb.Append("</table></section>");

            sb.Append("<section><h2>3. Vérifications</h2><table><thead><tr><th></th><th>Règle</th><th>Résultat</th><th>Référence</th></tr></thead><tbody>");
            foreach (var c in r.Checks)
            {
                var (cls, ic) = c.Level == CheckLevel.Ok ? ("ok", "✔") : c.Level == CheckLevel.Avertissement ? ("warn", "⚠") : c.Level == CheckLevel.Info ? ("info", "ℹ") : ("ko", "✖");
                sb.Append($"<tr class=\"{cls}\"><td class=\"ic\">{ic}</td><td>{H(c.Rule)}</td><td>{H(c.Message)}</td><td class=\"ref\">{H(c.Reference)}</td></tr>");
            }
            sb.Append("</tbody></table></section>");

            sb.Append("<section><h2>4. Nomenclature</h2><table><thead><tr><th>Repère</th><th>Désignation</th><th>Profilé</th><th class=\"n\">Long. débit</th><th class=\"n\">Qté</th><th class=\"n\">Masse</th></tr></thead><tbody>");
            foreach (var cp in CutPiece.FromModel(r.Model))
            {
                var p = ProfileCatalog.Get(cp.ProfileId);
                sb.Append($"<tr><td class=\"mk\">{H(Shorten(cp.Mark))}</td><td>{H(cp.Role)}</td><td>{H(p.Designation)}</td><td class=\"n\">{cp.Length.ToString("0", fr)}</td><td class=\"n\">{cp.Quantity}</td><td class=\"n\">{(p.MassPerMeter * cp.Length / 1000 * cp.Quantity).ToString("0.0", fr)} kg</td></tr>");
            }
            foreach (var g in r.Model.Plates.GroupBy(p => (p.Role, p.Thickness, L: Math.Round(p.BlankSize.L), W: Math.Round(p.BlankSize.W))))
                sb.Append($"<tr><td class=\"mk\">{H(Shorten(string.Join(",", g.Select(x => x.Mark))))}</td><td>{H(g.Key.Role)}</td><td>Tôle ép. {g.Key.Thickness.ToString("0.#", fr)} — {g.Key.L:0}x{g.Key.W:0}</td><td class=\"n\">{g.Key.L:0}</td><td class=\"n\">{g.Count()}</td><td class=\"n\">{g.Sum(x => x.Mass).ToString("0.0", fr)} kg</td></tr>");
            sb.Append("</tbody></table></section>");

            int sec = 5;
            if (nest != null)
            {
                sb.Append($"<section><h2>{sec++}. Plan de débit</h2>");
                foreach (var p in nest.Profiles)
                {
                    sb.Append($"<h3>{H(p.ProfileId)} — {p.NewBars} barre(s), rendement {p.Yield.ToString("P0", fr)}</h3>");
                    foreach (var b in p.Bars)
                    {
                        sb.Append("<div class=\"bar\">");
                        foreach (var c in b.Cuts)
                            sb.Append($"<span style=\"width:{(c.Length / b.StockLength * 100).ToString("0.##", CultureInfo.InvariantCulture)}%\" title=\"{H(c.Mark)} {c.Length:0}\">{H(c.Mark)} {c.Length:0}</span>");
                        sb.Append($"<i style=\"width:{(Math.Max(0, b.Remaining) / b.StockLength * 100).ToString("0.##", CultureInfo.InvariantCulture)}%\">{b.Remaining:0}</i></div>");
                    }
                }
                sb.Append($"<p class=\"note\">Trait de scie {nest.Settings.Kerf} mm, équerrage {nest.Settings.EndTrim} mm. Masse achetée : {nest.PurchasedMass.ToString("0.0", fr)} kg.</p></section>");
            }

            if (galvanized)
            {
                sb.Append($"<section><h2>{sec++}. Galvanisation — trous d'évent</h2><ul>");
                var adv = GalvanizingAdvisor.Advise(r.Model);
                if (adv.Count == 0) sb.Append("<li>Aucun profil creux.</li>");
                foreach (var a in adv) sb.Append($"<li>{H(a.ToString())}</li>");
                sb.Append("</ul><p class=\"note\">Trous diamétralement opposés, au plus près des extrémités ; aucun volume creux fermé. Référence : NF EN ISO 14713-2, à confirmer avec le galvaniseur.</p></section>");
            }

            if (cost != null)
            {
                sb.Append($"<section><h2>{sec++}. Estimation de prix (indicative)</h2><table><tbody>");
                foreach (var l in cost.Lines) sb.Append($"<tr><td>{H(l.Label)}</td><td>{H(l.Detail)}</td><td class=\"n\">{l.Amount.ToString("N2", fr)} €</td></tr>");
                sb.Append($"<tr class=\"tot\"><td colspan=\"2\">Total HT (marge incluse)</td><td class=\"n\">{cost.TotalHT.ToString("N2", fr)} €</td></tr>");
                sb.Append($"<tr class=\"tot\"><td colspan=\"2\">Total TTC</td><td class=\"n\">{cost.TotalTTC.ToString("N2", fr)} €</td></tr></tbody></table></section>");
            }

            // Normes réellement citées par les vérifications
            var used = Norms.All.Where(n => r.Checks.Any(c => c.Reference != null && c.Reference.Contains(n.Code.Split(':')[0]))).ToList();
            if (used.Count > 0)
            {
                sb.Append($"<section><h2>{sec++}. Textes de référence</h2>");
                foreach (var n in used)
                {
                    sb.Append($"<h3>{H(n.Code)} — {H(n.Title)}</h3><p class=\"note\">{H(n.Scope)} <b>Fiabilité :</b> {Conf(n.Confidence)}</p><ul>");
                    foreach (var v in n.Values) sb.Append($"<li>{H(v)}</li>");
                    sb.Append("</ul>");
                    if (n.Sources.Count > 0)
                        sb.Append("<p class=\"src\">Sources : ").Append(string.Join(" · ", n.Sources.Select(s => $"<a href=\"{H(s.Url)}\">{H(s.Label)}</a>"))).Append("</p>");
                }
                sb.Append("</section>");
            }

            sb.Append("<footer>Pré-dimensionnement et aide à la conception. Ne remplace ni la lecture des textes officiels (AFNOR, Légifrance), "
                    + "ni une note de calcul signée par un ingénieur, ni les essais (NF P01-013 pour les garde-corps). Valeurs à confirmer sur les éditions en vigueur.</footer>");
            sb.Append("</body></html>");
            return sb.ToString();
        }

        private static string Shorten(string marks)
        {
            var parts = (marks ?? "").Split(',');
            return parts.Length <= 4 ? marks : $"{parts[0]} … {parts[parts.Length - 1]} ({parts.Length})";
        }

        private static string Conf(NormConfidence c) =>
            c == NormConfidence.Concordant ? "sources secondaires concordantes" : c == NormConfidence.AConfirmer ? "à confirmer sur le texte officiel" : "règle de l'art";

        private const string Css = @"
:root{--ink:#1d2b44;--mut:#5b6678;--line:#d9dee7;--ok:#1d7a46;--warn:#a86400;--ko:#b3261e;--info:#2d5b9a;--bg:#fff}
*{box-sizing:border-box}body{font:14px/1.5 'Segoe UI',Arial,sans-serif;color:var(--ink);background:var(--bg);margin:0 auto;max-width:1000px;padding:24px 16px}
header{display:flex;justify-content:space-between;gap:16px;flex-wrap:wrap;border-bottom:3px solid var(--ink);padding-bottom:12px}
h1{margin:0;font-size:22px}.sub{margin:2px 0 0;color:var(--mut)}.meta{text-align:right;color:var(--mut);font-size:13px}
h2{font-size:17px;margin:28px 0 8px;border-bottom:1px solid var(--line);padding-bottom:4px}h3{font-size:14px;margin:14px 0 6px}
table{width:100%;border-collapse:collapse;font-size:13px}th,td{border-bottom:1px solid var(--line);padding:5px 6px;text-align:left;vertical-align:top}
thead th{background:#f3f5f9}.kv th{width:45%;font-weight:500;color:var(--mut)}.n{text-align:right;white-space:nowrap}.mk{max-width:180px;word-break:break-all}
tr.ok .ic{color:var(--ok)}tr.warn .ic{color:var(--warn)}tr.ko .ic{color:var(--ko)}tr.info .ic{color:var(--info)}tr.ko td{background:#fdf0ef}tr.warn td{background:#fff7ea}
.ic{width:24px;font-weight:bold}.ref{color:var(--mut);font-size:12px}.tot td{font-weight:600}
.verdict{margin:16px 0;padding:10px 14px;border-radius:6px;font-weight:600}.verdict.ok{background:#e8f5ee;color:var(--ok)}.verdict.warn{background:#fff3df;color:var(--warn)}.verdict.ko{background:#fdeceb;color:var(--ko)}
.fig svg{width:100%;height:auto;border:1px solid var(--line);border-radius:4px}
.bar{display:flex;height:22px;margin:3px 0;border:1px solid #9aa4b1;border-radius:3px;overflow:hidden;font-size:11px}
.bar span{background:#2d5b9a;color:#fff;border-right:2px solid #fff;white-space:nowrap;overflow:hidden;padding:2px 4px}.bar i{background:repeating-linear-gradient(45deg,#eee,#eee 4px,#ddd 4px,#ddd 8px);font-style:normal;color:#666;padding:2px 4px;overflow:hidden}
.note,.src{color:var(--mut);font-size:12.5px}.src a{color:var(--info)}
footer{margin-top:32px;padding-top:10px;border-top:1px solid var(--line);color:var(--mut);font-size:12px}
@media print{body{max-width:none;padding:0}section{break-inside:avoid}}";
    }
}
