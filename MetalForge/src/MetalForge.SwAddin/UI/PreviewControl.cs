using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using MetalForge.Core.Geometry;
using MetalForge.Core.Model;
using MetalForge.Core.Profiles;

namespace MetalForge.SwAddin.UI
{
    /// <summary>Aperçu 2D (vue de face XY) d'un ouvrage, avec cotes hors tout.</summary>
    public sealed class PreviewControl : Control
    {
        private StructureModel _model;
        private static readonly Color[] Palette =
        {
            Color.FromArgb(38, 70, 120), Color.FromArgb(200, 90, 40), Color.FromArgb(60, 140, 90),
            Color.FromArgb(120, 80, 160), Color.FromArgb(170, 140, 30), Color.FromArgb(40, 140, 160)
        };

        public PreviewControl()
        {
            DoubleBuffered = true;
            BackColor = Color.White;
            ResizeRedraw = true;
        }

        public StructureModel Model { get => _model; set { _model = value; Invalidate(); } }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            if (_model == null || (_model.Members.Count == 0 && _model.Plates.Count == 0))
            {
                TextRenderer.DrawText(g, "Aucun aperçu", Font, ClientRectangle, Color.Gray,
                                      TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            // Emprise
            var pts = _model.Members.SelectMany(m => new[] { m.Start, m.End }).Concat(_model.Plates.SelectMany(p => p.FrontProjection())).ToList();
            double minX = pts.Min(p => p.X), maxX = pts.Max(p => p.X), minY = pts.Min(p => p.Y), maxY = pts.Max(p => p.Y);
            double w = Math.Max(1, maxX - minX), h = Math.Max(1, maxY - minY);
            const int margin = 50;
            double k = Math.Min((Width - 2 * margin) / w, (Height - 2 * margin) / h);
            if (k <= 0) return;
            PointF P(Vec3 v) => new PointF((float)(margin + (v.X - minX) * k), (float)(Height - margin - (v.Y - minY) * k));

            // Tôles
            using (var fill = new SolidBrush(Color.FromArgb(70, 120, 120, 120)))
            using (var tread = new SolidBrush(Color.FromArgb(220, 59, 70, 86)))
            using (var pen = new Pen(Color.DimGray, 1))
                foreach (var pl in _model.Plates.OrderBy(p => p.Role == "Marche"))
                {
                    var poly = pl.FrontProjection().Select(P).ToArray();
                    g.FillPolygon(pl.Role == "Marche" ? tread : fill, poly); g.DrawPolygon(pen, poly);
                }

            // Barres : rectangle de largeur réelle dans le plan de vue
            var roles = _model.Members.Select(m => m.Role).Distinct().ToList();
            foreach (var m in _model.Members.OrderBy(m => m.Start.Z))
            {
                var d = new Vec3(m.End.X - m.Start.X, m.End.Y - m.Start.Y, 0);
                var color = Palette[roles.IndexOf(m.Role) % Palette.Length];
                if (d.Length < 1e-6)
                {
                    var c = P(m.Start); float r = (float)Math.Max(2, ProfileCatalog.Get(m.ProfileId).VisibleWidth * k / 2);
                    using (var b = new SolidBrush(color)) g.FillEllipse(b, c.X - r, c.Y - r, 2 * r, 2 * r);
                    continue;
                }
                var u = d.Normalized();
                var n = new Vec3(-u.Y, u.X, 0) * (ProfileCatalog.Get(m.ProfileId).VisibleWidth / 2);
                var a = new Vec3(m.Start.X, m.Start.Y, 0); var b2 = new Vec3(m.End.X, m.End.Y, 0);
                var quad = new[] { P(a + n), P(b2 + n), P(b2 - n), P(a - n) };
                using (var br = new SolidBrush(Color.FromArgb(200, color))) g.FillPolygon(br, quad);
                using (var pen = new Pen(Color.FromArgb(60, 60, 60), 0.5f)) g.DrawPolygon(pen, quad);
            }

            // Cotes hors tout
            using (var pen = new Pen(Color.Black, 1) { CustomStartCap = new AdjustableArrowCap(3, 4), CustomEndCap = new AdjustableArrowCap(3, 4) })
            using (var font = new Font("Segoe UI", 8.5f))
            {
                var p1 = P(new Vec3(minX, minY, 0)); var p2 = P(new Vec3(maxX, minY, 0)); var p3 = P(new Vec3(maxX, maxY, 0));
                float yd = p1.Y + 22, xd = p2.X + 22;
                g.DrawLine(pen, p1.X, yd, p2.X, yd);
                g.DrawLine(pen, xd, p2.Y, xd, p3.Y);
                var sw = $"{w:0}"; var sh = $"{h:0}";
                g.DrawString(sw, font, Brushes.Black, (p1.X + p2.X) / 2 - g.MeasureString(sw, font).Width / 2, yd + 2);
                var st = g.Save();
                g.TranslateTransform(xd + 4, (p2.Y + p3.Y) / 2 + g.MeasureString(sh, font).Width / 2);
                g.RotateTransform(-90);
                g.DrawString(sh, font, Brushes.Black, 0, 0);
                g.Restore(st);
            }

            // Légende
            using (var font = new Font("Segoe UI", 8f))
            {
                int y = 6;
                foreach (var r in roles)
                {
                    using (var b = new SolidBrush(Palette[roles.IndexOf(r) % Palette.Length])) g.FillRectangle(b, 6, y + 2, 10, 10);
                    g.DrawString($"{r} ({_model.Members.Count(m => m.Role == r)})", font, Brushes.Black, 20, y);
                    y += 15;
                }
            }
        }
    }
}
