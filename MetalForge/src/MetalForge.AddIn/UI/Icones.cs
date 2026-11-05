using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace MetalForge.AddIn.UI
{
    /// <summary>Icônes MetalForge dessinées par le code (aucun visuel tiers) : marches stylisées.</summary>
    internal static class Icones
    {
        /// <summary>Bitmap 16×18 du volet (format attendu par CreateTaskpaneView2), écrit dans %LOCALAPPDATA%\MetalForge.</summary>
        public static string VoletBmp()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MetalForge", "icones");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "volet.bmp");
            using (var bmp = new Bitmap(16, 18))
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                using (var acier = new SolidBrush(Color.FromArgb(60, 72, 88)))
                using (var minium = new SolidBrush(Color.FromArgb(214, 80, 40)))
                {
                    for (int i = 0; i < 4; i++) g.FillRectangle(acier, 1 + i * 3, 14 - i * 3, 15 - i * 3, 3);
                    g.FillRectangle(minium, 1, 1, 2, 16); // limon
                }
                bmp.Save(path, ImageFormat.Bmp);
            }
            return path;
        }
    }
}
