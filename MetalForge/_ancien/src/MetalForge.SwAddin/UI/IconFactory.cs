using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace MetalForge.SwAddin.UI
{
    /// <summary>
    /// Génère les bandes d'icônes PNG des commandes (aucune ressource binaire à maintenir) :
    /// pictogrammes vectoriels simples dessinés en GDI+, aux tailles demandées par SolidWorks.
    /// </summary>
    public static class IconFactory
    {
        public enum Glyph { Atomic, Stair, Railing, Gate, Ladder, CutList, Profiles, Settings, Logo }

        private static readonly int[] Sizes = { 20, 32, 40, 64, 96, 128 };

        public static string Folder => Path.Combine(Path.GetTempPath(), "MetalForge", "icons");

        /// <summary>Crée une bande par taille ; retourne les chemins dans l'ordre attendu par IconList.</summary>
        public static string[] Strips(Glyph[] glyphs)
        {
            Directory.CreateDirectory(Folder);
            return Sizes.Select(s =>
            {
                var path = Path.Combine(Folder, $"strip_{s}.png");
                using (var bmp = new Bitmap(s * glyphs.Length, s, PixelFormat.Format32bppArgb))
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    for (int i = 0; i < glyphs.Length; i++) Draw(g, glyphs[i], new RectangleF(i * s, 0, s, s));
                    bmp.Save(path, ImageFormat.Png);
                }
                return path;
            }).ToArray();
        }

        public static string[] Main() => Sizes.Select(s =>
        {
            Directory.CreateDirectory(Folder);
            var path = Path.Combine(Folder, $"main_{s}.png");
            using (var bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                Draw(g, Glyph.Logo, new RectangleF(0, 0, s, s));
                bmp.Save(path, ImageFormat.Png);
            }
            return path;
        }).ToArray();

        /// <summary>BMP 16x18 pour l'onglet du volet des tâches (CreateTaskpaneView2).</summary>
        public static string TaskPaneBitmap()
        {
            Directory.CreateDirectory(Folder);
            var path = Path.Combine(Folder, "taskpane.bmp");
            using (var bmp = new Bitmap(16, 18))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.White);
                Draw(g, Glyph.Logo, new RectangleF(0, 1, 16, 16));
                bmp.Save(path, ImageFormat.Bmp);
            }
            return path;
        }

        private static void Draw(Graphics g, Glyph glyph, RectangleF r)
        {
            float u = r.Width / 20f; // unité de dessin : grille 20x20
            PointF P(float x, float y) => new PointF(r.X + x * u, r.Y + y * u);
            var steel = Color.FromArgb(45, 75, 120);
            var accent = Color.FromArgb(220, 110, 30);
            using (var pen = new Pen(steel, Math.Max(1f, 1.6f * u)) { StartCap = LineCap.Square, EndCap = LineCap.Square })
            using (var thin = new Pen(steel, Math.Max(1f, 1.0f * u)))
            using (var acc = new Pen(accent, Math.Max(1f, 1.6f * u)))
            {
                switch (glyph)
                {
                    case Glyph.Stair:
                        g.DrawLines(pen, new[] { P(2, 18), P(6, 18), P(6, 14), P(10, 14), P(10, 10), P(14, 10), P(14, 6), P(18, 6), P(18, 2) });
                        g.DrawLine(acc, P(2, 16), P(17, 1));
                        break;
                    case Glyph.Railing:
                        g.DrawLine(pen, P(2, 4), P(18, 4));
                        g.DrawLine(pen, P(3, 4), P(3, 18)); g.DrawLine(pen, P(17, 4), P(17, 18));
                        for (int x = 6; x <= 14; x += 3) g.DrawLine(thin, P(x, 5), P(x, 16));
                        g.DrawLine(acc, P(2, 16), P(18, 16));
                        break;
                    case Glyph.Gate:
                        g.DrawRectangle(pen, r.X + 2 * u, r.Y + 5 * u, 7 * u, 12 * u);
                        g.DrawRectangle(pen, r.X + 11 * u, r.Y + 5 * u, 7 * u, 12 * u);
                        for (int x = 4; x <= 7; x += 3) g.DrawLine(thin, P(x, 6), P(x, 16));
                        for (int x = 13; x <= 16; x += 3) g.DrawLine(thin, P(x, 6), P(x, 16));
                        g.DrawLine(acc, P(1, 18.5f), P(19, 18.5f));
                        break;
                    case Glyph.Atomic: // briques élémentaires assemblées
                        g.DrawLine(thin, P(5, 5), P(15, 9)); g.DrawLine(thin, P(5, 5), P(7, 15)); g.DrawLine(thin, P(15, 9), P(7, 15));
                        using (var sb = new SolidBrush(steel)) { g.FillRectangle(sb, r.X + 2 * u, r.Y + 2 * u, 6 * u, 6 * u); g.FillRectangle(sb, r.X + 4 * u, r.Y + 12 * u, 6 * u, 6 * u); }
                        using (var ab = new SolidBrush(accent)) g.FillEllipse(ab, r.X + 12 * u, r.Y + 6 * u, 6 * u, 6 * u);
                        break;
                    case Glyph.Ladder:
                        g.DrawLine(pen, P(5, 1), P(5, 19)); g.DrawLine(pen, P(11, 1), P(11, 19));
                        for (int y = 4; y <= 18; y += 4) g.DrawLine(thin, P(5, y), P(11, y));
                        g.DrawArc(acc, r.X + 3 * u, r.Y + 4 * u, 14 * u, 8 * u, -60, 210);
                        g.DrawArc(acc, r.X + 3 * u, r.Y + 10 * u, 14 * u, 8 * u, -60, 210);
                        break;
                    case Glyph.CutList:
                        g.DrawRectangle(pen, r.X + 2 * u, r.Y + 4 * u, 16 * u, 3 * u);
                        g.DrawRectangle(pen, r.X + 2 * u, r.Y + 9 * u, 16 * u, 3 * u);
                        g.DrawRectangle(pen, r.X + 2 * u, r.Y + 14 * u, 16 * u, 3 * u);
                        g.DrawLine(acc, P(11, 3), P(11, 8)); g.DrawLine(acc, P(7, 8), P(7, 13)); g.DrawLine(acc, P(14, 13), P(14, 18));
                        break;
                    case Glyph.Profiles:
                        g.DrawLines(pen, new[] { P(3, 3), P(9, 3), P(9, 4.5f), P(6.8f, 4.5f), P(6.8f, 15.5f), P(9, 15.5f), P(9, 17), P(3, 17), P(3, 15.5f), P(5.2f, 15.5f), P(5.2f, 4.5f), P(3, 4.5f), P(3, 3) });
                        g.DrawRectangle(pen, r.X + 11 * u, r.Y + 3 * u, 6 * u, 6 * u);
                        g.DrawEllipse(acc, r.X + 11 * u, r.Y + 11 * u, 6 * u, 6 * u);
                        break;
                    case Glyph.Settings:
                        g.DrawEllipse(pen, r.X + 6 * u, r.Y + 6 * u, 8 * u, 8 * u);
                        for (int a = 0; a < 360; a += 45)
                        {
                            double rad = a * Math.PI / 180;
                            g.DrawLine(pen, P(10 + (float)(5.5 * Math.Cos(rad)), 10 + (float)(5.5 * Math.Sin(rad))),
                                            P(10 + (float)(8 * Math.Cos(rad)), 10 + (float)(8 * Math.Sin(rad))));
                        }
                        using (var ab = new SolidBrush(accent)) g.FillEllipse(ab, r.X + 8.5f * u, r.Y + 8.5f * u, 3 * u, 3 * u);
                        break;
                    case Glyph.Logo:
                        using (var b = new SolidBrush(steel)) g.FillRectangle(b, r.X + 1 * u, r.Y + 1 * u, 18 * u, 18 * u);
                        using (var w = new Pen(Color.White, Math.Max(1f, 2f * u)))
                            g.DrawLines(w, new[] { P(4, 16), P(4, 5), P(10, 11), P(16, 5), P(16, 16) });
                        g.DrawLine(acc, P(2, 18), P(18, 18));
                        break;
                }
            }
        }
    }
}
