using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace MetalForge.Core.Profiles
{
    /// <summary>
    /// Bibliothèque de profilés acier courants en métallerie-serrurerie.
    /// Identifiants : TC40x40x2, TR60x40x3, TRD42.4x2, PL40x8, CP12, RP12, L40x40x4, T40x40x5, UPN120, IPE200, HEA140.
    /// Les identifiants non listés mais bien formés sont acceptés (section calculée).
    /// </summary>
    public static class ProfileCatalog
    {
        private static readonly Dictionary<string, Profile> _items = new Dictionary<string, Profile>(StringComparer.OrdinalIgnoreCase);

        static ProfileCatalog()
        {
            // Tubes carrés EN 10219 (formage à froid)
            foreach (var (b, t) in new[] { (20, 1.5), (20, 2.0), (25, 2.0), (30, 2.0), (30, 3.0), (35, 2.0), (40, 2.0), (40, 3.0),
                                           (40, 4.0), (50, 2.0), (50, 3.0), (50, 4.0), (60, 3.0), (60, 4.0), (70, 3.0), (80, 3.0),
                                           (80, 4.0), (80, 5.0), (100, 4.0), (100, 5.0), (120, 5.0), (120, 6.0) })
                Add(new Profile { Id = Id("TC", b, b, t), Family = ProfileFamily.TubeCarre, B = b, H = b, T = t });

            // Tubes rectangulaires EN 10219 (H x B x t)
            foreach (var (h, b, t) in new[] { (30, 20, 2.0), (40, 20, 2.0), (40, 27, 2.0), (50, 25, 2.0), (50, 30, 2.0), (60, 30, 2.0),
                                              (60, 40, 2.0), (60, 40, 3.0), (80, 40, 2.0), (80, 40, 3.0), (80, 40, 4.0), (100, 40, 3.0),
                                              (100, 50, 3.0), (100, 50, 4.0), (120, 60, 4.0), (120, 80, 4.0), (150, 100, 5.0) })
                Add(new Profile { Id = Id("TR", h, b, t), Family = ProfileFamily.TubeRectangulaire, H = h, B = b, T = t });

            // Tubes ronds EN 10255 / EN 10219
            foreach (var (d, t) in new[] { (21.3, 2.0), (26.9, 2.0), (33.7, 2.0), (42.4, 2.0), (42.4, 2.6), (48.3, 2.0),
                                           (48.3, 3.0), (60.3, 3.0), (76.1, 3.0), (88.9, 3.2), (114.3, 3.6) })
                Add(new Profile { Id = "TRD" + F(d) + "x" + F(t), Family = ProfileFamily.TubeRond, B = d, H = d, T = t });

            // Plats EN 10058
            foreach (var (b, t) in new[] { (20, 4), (25, 5), (30, 5), (30, 8), (40, 5), (40, 8), (40, 10), (50, 5), (50, 8), (50, 10),
                                           (60, 8), (60, 10), (80, 8), (80, 10), (100, 10), (120, 10), (150, 10), (200, 10), (250, 12) })
                Add(new Profile { Id = "PL" + b + "x" + t, Family = ProfileFamily.Plat, B = b, H = t, T = t });

            // Carrés / ronds pleins
            foreach (var a in new[] { 10, 12, 14, 16, 20, 25 })
            {
                Add(new Profile { Id = "CP" + a, Family = ProfileFamily.CarrePlein, B = a, H = a, T = a });
                Add(new Profile { Id = "RP" + a, Family = ProfileFamily.RondPlein, B = a, H = a, T = a });
            }

            // Cornières à ailes égales EN 10056 (masses tabulées)
            foreach (var (a, t, m) in new[] { (20, 3, 0.882), (25, 3, 1.12), (30, 3, 1.36), (30, 4, 1.78), (35, 4, 2.09), (40, 4, 2.42),
                                              (40, 5, 2.97), (50, 5, 3.77), (50, 6, 4.47), (60, 6, 5.42), (70, 7, 7.38), (80, 8, 9.63), (100, 10, 15.0) })
                Add(new Profile { Id = Id("L", a, a, t), Family = ProfileFamily.Corniere, B = a, H = a, T = t, TabulatedMassPerMeter = m });

            // Fers T EN 10055
            foreach (var (a, t, m) in new[] { (30, 4, 1.77), (35, 4.5, 2.33), (40, 5, 2.96), (50, 6, 4.44), (60, 7, 6.23) })
                Add(new Profile { Id = Id("T", a, a, t), Family = ProfileFamily.FerT, B = a, H = a, T = t, TabulatedMassPerMeter = m });

            // UPN (DIN 1026-1) : h, b, tw, tf, kg/m
            foreach (var (h, b, tw, tf, m) in new[] { (80, 45, 6.0, 8.0, 8.64), (100, 50, 6.0, 8.5, 10.6), (120, 55, 7.0, 9.0, 13.4),
                                                      (140, 60, 7.0, 10.0, 16.0), (160, 65, 7.5, 10.5, 18.8), (180, 70, 8.0, 11.0, 22.0),
                                                      (200, 75, 8.5, 11.5, 25.3) })
                Add(new Profile { Id = "UPN" + h, Family = ProfileFamily.UPN, H = h, B = b, T = tw, Tf = tf, TabulatedMassPerMeter = m });

            // IPE (EN 10365)
            foreach (var (h, b, tw, tf, m) in new[] { (80, 46, 3.8, 5.2, 6.0), (100, 55, 4.1, 5.7, 8.1), (120, 64, 4.4, 6.3, 10.4),
                                                      (140, 73, 4.7, 6.9, 12.9), (160, 82, 5.0, 7.4, 15.8), (180, 91, 5.3, 8.0, 18.8),
                                                      (200, 100, 5.6, 8.5, 22.4), (220, 110, 5.9, 9.2, 26.2), (240, 120, 6.2, 9.8, 30.7),
                                                      (270, 135, 6.6, 10.2, 36.1), (300, 150, 7.1, 10.7, 42.2) })
                Add(new Profile { Id = "IPE" + h, Family = ProfileFamily.IPE, H = h, B = b, T = tw, Tf = tf, TabulatedMassPerMeter = m });

            // HEA (EN 10365) : désignation, h réel, b, tw, tf, kg/m
            foreach (var (n, h, b, tw, tf, m) in new[] { (100, 96, 100, 5.0, 8.0, 16.7), (120, 114, 120, 5.0, 8.0, 19.9),
                                                         (140, 133, 140, 5.5, 8.5, 24.7), (160, 152, 160, 6.0, 9.0, 30.4),
                                                         (180, 171, 180, 6.0, 9.5, 35.5), (200, 190, 200, 6.5, 10.0, 42.3) })
                Add(new Profile { Id = "HEA" + n, Family = ProfileFamily.HEA, H = h, B = b, T = tw, Tf = tf, TabulatedMassPerMeter = m });
        }

        public static IReadOnlyCollection<Profile> All => _items.Values;

        public static IEnumerable<string> Ids(ProfileFamily family) =>
            _items.Values.Where(p => p.Family == family).Select(p => p.Id);

        public static bool TryGet(string id, out Profile profile)
        {
            profile = null;
            if (string.IsNullOrWhiteSpace(id)) return false;
            id = id.Trim().Replace(" ", "").Replace(',', '.');
            if (_items.TryGetValue(id, out profile)) return true;
            profile = Parse(id);
            return profile != null;
        }

        public static Profile Get(string id)
        {
            if (TryGet(id, out var p)) return p;
            throw new ArgumentException($"Profilé inconnu : '{id}'. Exemples valides : TC40x40x2, TR60x40x2, TRD42.4x2, PL40x8, RP12, L40x40x4, UPN120, IPE200.");
        }

        private static readonly Regex Num = new Regex(@"^(?<p>TC|TR|TRD|PL|CP|RP|L|T)(?<a>\d+(\.\d+)?)(x(?<b>\d+(\.\d+)?))?(x(?<c>\d+(\.\d+)?))?$",
                                                      RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>Crée un profilé hors catalogue à partir de son identifiant (sections paramétriques uniquement).</summary>
        private static Profile Parse(string id)
        {
            var m = Num.Match(id);
            if (!m.Success) return null;
            double D(string g) => m.Groups[g].Success ? double.Parse(m.Groups[g].Value, CultureInfo.InvariantCulture) : 0;
            double a = D("a"), b = D("b"), c = D("c");
            var prefix = m.Groups["p"].Value.ToUpperInvariant();
            Profile p = null;
            switch (prefix)
            {
                case "TC" when c > 0: p = new Profile { Family = ProfileFamily.TubeCarre, B = a, H = b, T = c }; break;
                case "TC" when b > 0: p = new Profile { Family = ProfileFamily.TubeCarre, B = a, H = a, T = b }; break;
                case "TR" when c > 0: p = new Profile { Family = ProfileFamily.TubeRectangulaire, H = a, B = b, T = c }; break;
                case "TRD" when b > 0: p = new Profile { Family = ProfileFamily.TubeRond, B = a, H = a, T = b }; break;
                case "PL" when b > 0: p = new Profile { Family = ProfileFamily.Plat, B = a, H = b, T = b }; break;
                case "CP": p = new Profile { Family = ProfileFamily.CarrePlein, B = a, H = a, T = a }; break;
                case "RP": p = new Profile { Family = ProfileFamily.RondPlein, B = a, H = a, T = a }; break;
                case "L" when c > 0: p = new Profile { Family = ProfileFamily.Corniere, B = a, H = b, T = c }; break;
                case "T" when c > 0: p = new Profile { Family = ProfileFamily.FerT, B = a, H = b, T = c }; break;
            }
            if (p == null || p.Area <= 0) return null;
            p.Id = id.ToUpperInvariant();
            return p;
        }

        private static void Add(Profile p) => _items[p.Id] = p;
        private static string F(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);
        private static string Id(string prefix, double a, double b, double t) => prefix + F(a) + "x" + F(b) + "x" + F(t);
    }
}
