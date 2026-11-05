using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MetalForge.Core.Profiles
{
    /// <summary>
    /// Reconnaît un profilé à partir d'un texte libre : description de liste des pièces soudées SolidWorks
    /// (« SQUARE TUBE 40 X 40 X 4 », « Tube carré 40x40x2 »…) ou chemin de fichier .sldlfp
    /// (« iso\square tube\40 x 40 x 4.sldlfp »). Retourne l'identifiant catalogue (TC40x40x4) ou null.
    /// </summary>
    public static class ProfileNameMatcher
    {
        private static readonly Regex Numbers = new Regex(@"\d+(?:[.,]\d+)?", RegexOptions.CultureInvariant);

        public static string Match(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var t = Normalize(text);
            var n = Numbers.Matches(t).Cast<System.Text.RegularExpressions.Match>()
                           .Select(m => double.Parse(m.Value.Replace(',', '.'), CultureInfo.InvariantCulture)).ToArray();
            if (n.Length == 0) return null;
            string f(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);

            bool Has(params string[] keys) => keys.Any(k => Regex.IsMatch(t, @"(^|[^a-z])" + k));

            if (Has("ipe")) return "IPE" + f(n[0]);
            if (Has("hea")) return "HEA" + f(n[0]);
            if (Has("upn", "upe", "c channel", "channel", "fer u", "u ")) return "UPN" + f(n[0]);

            bool tube = Has("tube", "tub", "hollow", "rhs", "shs", "chs", "pipe");
            if (Has("pipe", "chs", "rond") && tube && n.Length >= 2) return "TRD" + f(n[0]) + "x" + f(n[1]);
            if (Has("rect", "rhs") && n.Length >= 3) return "TR" + f(Math.Max(n[0], n[1])) + "x" + f(Math.Min(n[0], n[1])) + "x" + f(n[2]);
            if (Has("square", "carre", "shs") && tube && n.Length >= 3) return "TC" + f(n[0]) + "x" + f(n[1]) + "x" + f(n[2]);
            if (tube && n.Length >= 3) return n[0] == n[1] ? "TC" + f(n[0]) + "x" + f(n[1]) + "x" + f(n[2])
                                                           : "TR" + f(Math.Max(n[0], n[1])) + "x" + f(Math.Min(n[0], n[1])) + "x" + f(n[2]);
            if (tube && n.Length == 2) return "TRD" + f(n[0]) + "x" + f(n[1]);

            if (Has("angle", "corniere", "cornie") && n.Length >= 3) return "L" + f(n[0]) + "x" + f(n[1]) + "x" + f(n[2]);
            if (Regex.IsMatch(t, @"^l\s*\d") && n.Length >= 3) return "L" + f(n[0]) + "x" + f(n[1]) + "x" + f(n[2]);
            if (Has("angle", "corniere") && n.Length == 2) return "L" + f(n[0]) + "x" + f(n[0]) + "x" + f(n[1]);
            if (Has("fer t", "tee", "t-bar") && n.Length >= 3) return "T" + f(n[0]) + "x" + f(n[1]) + "x" + f(n[2]);
            if (Has("flat", "plat", "fer plat") && n.Length >= 2) return "PL" + f(Math.Max(n[0], n[1])) + "x" + f(Math.Min(n[0], n[1]));
            if (Has("round bar", "rond plein", "rond")) return "RP" + f(n[0]);
            if (Has("square bar", "carre plein", "carre")) return "CP" + f(n[0]);

            // Désignation catalogue directe (TC40x40x2…)
            var compact = t.Replace(" ", "").ToUpperInvariant();
            return ProfileCatalog.TryGet(compact, out var p) ? p.Id : null;
        }

        /// <summary>Minuscule, sans accent, « 40 X 40 » → « 40 x 40 », séparateurs de chemin → espace.</summary>
        public static string Normalize(string s)
        {
            var d = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in d)
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
            var r = sb.ToString().ToLowerInvariant().Replace('\\', ' ').Replace('/', ' ').Replace('_', ' ').Replace('ø', ' ');
            r = Regex.Replace(r, @"\.sldlfp$", "");
            return Regex.Replace(r, @"\s+", " ").Trim();
        }
    }
}
