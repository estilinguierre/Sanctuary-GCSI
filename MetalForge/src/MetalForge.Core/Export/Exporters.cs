using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MetalForge.Core.Fabrication;
using MetalForge.Core.Geometry;
using MetalForge.Core.Model;
using MetalForge.Core.Profiles;

namespace MetalForge.Core.Export
{
    /// <summary>Nomenclature CSV (séparateur « ; », décimales françaises : ouverture directe dans Excel FR).</summary>
    public static class CsvExporter
    {
        private static readonly CultureInfo Fr = new CultureInfo("fr-FR");

        public static string BillOfMaterials(StructureModel model)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Repère;Désignation;Profilé;Longueur débit (mm);Qté;Masse unit. (kg);Masse totale (kg);Surface (m²)");
            foreach (var cp in CutPiece.FromModel(model))
            {
                var p = ProfileCatalog.Get(cp.ProfileId);
                double mu = p.MassPerMeter * cp.Length / 1000;
                double s = p.PaintPerimeter * cp.Length * 1e-6;
                sb.AppendLine(string.Join(";", Esc(cp.Mark), Esc(cp.Role), Esc(p.Designation), F(cp.Length, "0.0"), cp.Quantity,
                                          F(mu, "0.00"), F(mu * cp.Quantity, "0.00"), F(s * cp.Quantity, "0.000")));
            }
            foreach (var g in model.Plates.GroupBy(p => (p.Role, p.Thickness, L: System.Math.Round(p.BlankSize.L), W: System.Math.Round(p.BlankSize.W))))
            {
                var first = g.First();
                sb.AppendLine(string.Join(";", Esc(string.Join(",", g.Select(x => x.Mark))), Esc(g.Key.Role),
                                          Esc($"Tôle ép. {g.Key.Thickness:0.#} — {g.Key.L:0}x{g.Key.W:0}"), F(g.Key.L, "0"), g.Count(),
                                          F(first.Mass, "0.00"), F(first.Mass * g.Count(), "0.00"), ""));
            }
            sb.AppendLine($";;;;;TOTAL;{F(model.TotalMass, "0.00")};{F(model.TotalPaintArea, "0.000")}");
            return sb.ToString();
        }

        public static string Nesting(NestingResult r)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Profilé;Barre n°;Longueur barre;Origine;Coupes (repère:longueur);Reste (mm)");
            foreach (var p in r.Profiles)
            {
                int i = 0;
                foreach (var b in p.Bars)
                    sb.AppendLine(string.Join(";", Esc(p.ProfileId), ++i, F(b.StockLength, "0"), b.FromOffcut ? "Chute" : "Neuve",
                                              Esc(string.Join(" ", b.Cuts.Select(c => $"{c.Mark}:{c.Length.ToString("0.#", Fr)}"))), F(b.Remaining, "0")));
            }
            return sb.ToString();
        }

        public static void Save(string path, string csv) => File.WriteAllText(path, csv, new UTF8Encoding(true)); // BOM pour Excel

        private static string F(double v, string fmt) => v.ToString(fmt, Fr);
        private static string Esc(string s) => s != null && (s.Contains(";") || s.Contains("\"")) ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    /// <summary>Écrivain DXF minimal (AC1009 / R12, entités LINE et TEXT) — lu par tous les logiciels de découpe et de CAO.</summary>
    public sealed class DxfWriter
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public DxfWriter()
        {
            Pair(0, "SECTION"); Pair(2, "HEADER");
            Pair(9, "$ACADVER"); Pair(1, "AC1009");
            Pair(9, "$INSUNITS"); Pair(70, "4"); // mm
            Pair(0, "ENDSEC");
            Pair(0, "SECTION"); Pair(2, "ENTITIES");
        }

        public DxfWriter Line(Vec3 a, Vec3 b, string layer = "0")
        {
            Pair(0, "LINE"); Pair(8, Ascii(layer));
            Pair(10, N(a.X)); Pair(20, N(a.Y)); Pair(30, "0.0");
            Pair(11, N(b.X)); Pair(21, N(b.Y)); Pair(31, "0.0");
            return this;
        }

        public DxfWriter Polygon(IEnumerable<Vec3> pts, string layer = "0")
        {
            var l = pts.ToList();
            for (int i = 0; i < l.Count; i++) Line(l[i], l[(i + 1) % l.Count], layer);
            return this;
        }

        public DxfWriter Text(Vec3 at, string text, double height = 10, string layer = "TEXTE")
        {
            Pair(0, "TEXT"); Pair(8, Ascii(layer));
            Pair(10, N(at.X)); Pair(20, N(at.Y)); Pair(30, "0.0");
            Pair(40, N(height)); Pair(1, Ascii(text));
            return this;
        }

        public override string ToString() => _sb.ToString() + "  0\nENDSEC\n  0\nEOF\n";

        public void Save(string path) => File.WriteAllText(path, ToString(), Encoding.ASCII);

        /// <summary>R12 = ASCII : on retire les accents (é → e) des calques et textes.</summary>
        private static string Ascii(string s)
        {
            var d = (s ?? "").Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in d)
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch < 128 ? ch : '_');
            return sb.ToString();
        }

        private void Pair(int code, string value) => _sb.Append(code.ToString().PadLeft(3)).Append('\n').Append(value).Append('\n');
        private static string N(double v) => v.ToString("0.0###", Inv);
    }

    /// <summary>Plans de traçage DXF générés par les assistants.</summary>
    public static class DxfDrawings
    {
        /// <summary>Limon tôle développé : contour de découpe + traits des marches (calque TRACAGE).</summary>
        public static DxfWriter Stringer(Stairs.StairDesigner designer)
        {
            var g = designer.Solve();
            var o = designer.StringerOutline(g);
            var dxf = new DxfWriter();
            dxf.Polygon(o.Polygon, "DECOUPE");
            int i = 0;
            foreach (var (front, back) in o.TreadMarks)
            {
                i++;
                dxf.Line(front, back, "TRACAGE");
                dxf.Line(new Vec3(front.X, front.Y - designer.TreadDepthVertical, 0), new Vec3(back.X, back.Y - designer.TreadDepthVertical, 0), "TRACAGE");
                dxf.Text(new Vec3(front.X + 5, front.Y + 5, 0), "M" + i, 12);
            }
            dxf.Text(new Vec3(o.BottomStart.X, -60, 0),
                     $"LIMON {designer.P.StringerProfile} - {g.Treads} marches h={g.Riser:0.0} g={g.Going:0.0} - x2 (gauche/droite symétriques)", 15);
            return dxf;
        }

        /// <summary>Vue de face filaire (XY) d'un ouvrage : contrôle rapide ou fond de plan.</summary>
        public static DxfWriter FrontView(StructureModel model)
        {
            var dxf = new DxfWriter();
            foreach (var m in model.Members)
            {
                var p = ProfileCatalog.Get(m.ProfileId);
                var d = (m.End - m.Start); var u = new Vec3(d.X, d.Y, 0).Normalized();
                if (u.Length < 1e-9) continue; // barre perpendiculaire au plan de vue
                var n = new Vec3(-u.Y, u.X, 0) * (p.VisibleWidth / 2);
                dxf.Polygon(new[] { Flat(m.Start) + n, Flat(m.End) + n, Flat(m.End) - n, Flat(m.Start) - n }, m.Role.ToUpperInvariant().Replace(' ', '_'));
            }
            foreach (var pl in model.Plates) dxf.Polygon(pl.Outline, "TOLE");
            return dxf;
        }

        private static Vec3 Flat(Vec3 v) => new Vec3(v.X, v.Y, 0);
    }

    /// <summary>Vue de face SVG (devis, documentation, aperçu web) avec cotes hors tout.</summary>
    public static class SvgDrawings
    {
        private static readonly string[] Palette = { "#264678", "#c85a28", "#3c8c5a", "#7850a0", "#aa8c1e", "#288ca0" };

        public static string FrontView(StructureModel model, int width = 900, int height = 600)
        {
            var inv = CultureInfo.InvariantCulture;
            var pts = model.Members.SelectMany(m => new[] { m.Start, m.End }).Concat(model.Plates.SelectMany(p => p.Outline)).ToList();
            double minX = pts.Min(p => p.X), maxX = pts.Max(p => p.X), minY = pts.Min(p => p.Y), maxY = pts.Max(p => p.Y);
            double w = System.Math.Max(1, maxX - minX), h = System.Math.Max(1, maxY - minY);
            const int margin = 60;
            double k = System.Math.Min((width - 2 * margin) / w, (height - 2 * margin - 30) / h);
            string P(Vec3 v) => string.Format(inv, "{0:0.#},{1:0.#}", margin + (v.X - minX) * k, height - margin - (v.Y - minY) * k);

            var sb = new StringBuilder();
            sb.AppendLine(string.Format(inv, "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {0} {1}\" font-family=\"Segoe UI, Arial, sans-serif\" font-size=\"13\">", width, height));
            sb.AppendLine($"<rect width=\"100%\" height=\"100%\" fill=\"#ffffff\"/>");
            sb.AppendLine($"<text x=\"{margin}\" y=\"28\" font-size=\"16\" font-weight=\"bold\" fill=\"#1d2b44\">{Xml(model.Name)}</text>");
            foreach (var pl in model.Plates)
                sb.AppendLine($"<polygon points=\"{string.Join(" ", pl.Outline.Select(P))}\" fill=\"{(pl.Role == "Marche" ? "#3b4656" : "#9aa4b1")}\" fill-opacity=\"{(pl.Role == "Marche" ? "0.9" : "0.55")}\" stroke=\"#4a5361\" stroke-width=\"0.8\"/>");
            var roles = model.Members.Select(m => m.Role).Distinct().ToList();
            foreach (var m in model.Members.OrderBy(m => m.Start.Z))
            {
                var d = new Vec3(m.End.X - m.Start.X, m.End.Y - m.Start.Y, 0);
                if (d.Length < 1e-6) continue;
                var u = d.Normalized();
                var n = new Vec3(-u.Y, u.X, 0) * (ProfileCatalog.Get(m.ProfileId).VisibleWidth / 2);
                var a = new Vec3(m.Start.X, m.Start.Y, 0); var b = new Vec3(m.End.X, m.End.Y, 0);
                sb.AppendLine($"<polygon points=\"{P(a + n)} {P(b + n)} {P(b - n)} {P(a - n)}\" fill=\"{Palette[roles.IndexOf(m.Role) % Palette.Length]}\" fill-opacity=\"0.85\" stroke=\"#333\" stroke-width=\"0.4\"/>");
            }
            // cotes
            double y0 = height - margin + 22, x1 = margin, x2 = margin + w * k, xr = x2 + 22, yt = height - margin - h * k, yb = height - margin;
            sb.AppendLine(string.Format(inv, "<g stroke=\"#000\" stroke-width=\"1\"><line x1=\"{0:0.#}\" y1=\"{1:0.#}\" x2=\"{2:0.#}\" y2=\"{1:0.#}\"/><line x1=\"{3:0.#}\" y1=\"{4:0.#}\" x2=\"{3:0.#}\" y2=\"{5:0.#}\"/></g>", x1, y0, x2, xr, yt, yb));
            sb.AppendLine(string.Format(inv, "<text x=\"{0:0.#}\" y=\"{1:0.#}\" text-anchor=\"middle\">{2:0} mm</text>", (x1 + x2) / 2, y0 + 16, w));
            sb.AppendLine(string.Format(inv, "<text transform=\"translate({0:0.#},{1:0.#}) rotate(-90)\" text-anchor=\"middle\">{2:0} mm</text>", xr + 16, (yt + yb) / 2, h));
            // légende
            int ly = 48;
            foreach (var r in roles)
            {
                sb.AppendLine($"<rect x=\"{width - 200}\" y=\"{ly - 10}\" width=\"12\" height=\"12\" fill=\"{Palette[roles.IndexOf(r) % Palette.Length]}\"/><text x=\"{width - 182}\" y=\"{ly}\">{Xml(r)} ({model.Members.Count(m => m.Role == r)})</text>");
                ly += 18;
            }
            sb.AppendLine("</svg>");
            return sb.ToString();
        }

        private static string Xml(string s) => System.Security.SecurityElement.Escape(s ?? "");
    }
}
