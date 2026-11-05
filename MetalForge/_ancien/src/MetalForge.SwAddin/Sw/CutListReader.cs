using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using MetalForge.Core.Fabrication;
using MetalForge.Core.Geometry;
using MetalForge.Core.Model;
using MetalForge.Core.Profiles;
using MetalForge.SwAddin.Settings;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace MetalForge.SwAddin.Sw
{
    /// <summary>Contenu de la liste des pièces soudées d'une pièce SolidWorks active.</summary>
    public sealed class SwCutList
    {
        public List<CutPiece> Pieces { get; } = new List<CutPiece>();
        public List<string> Unmatched { get; } = new List<string>();   // corps sans profil reconnu (tôles, profils perso)

        /// <summary>Modèle neutre équivalent (barres posées sur X) pour réutiliser débit et chiffrage.</summary>
        public StructureModel ToModel(string name)
        {
            var m = new StructureModel { Name = name };
            foreach (var p in Pieces)
            {
                var marks = (p.Mark ?? "").Split(',');
                for (int i = 0; i < p.Quantity; i++)
                {
                    var mem = m.AddMember("SW", p.Role, p.ProfileId, Vec3.Zero, new Vec3(p.Length, 0, 0));
                    if (marks.Length == p.Quantity && marks[i].Length > 0) mem.Mark = marks[i];
                }
            }
            return m;
        }
    }

    /// <summary>Lit les dossiers de la liste des pièces soudées (CutListFolder) de la pièce active.</summary>
    public sealed class CutListReader
    {
        private readonly AddinSettings _settings;
        public CutListReader(AddinSettings settings) { _settings = settings; }

        public SwCutList Read(ModelDoc2 doc)
        {
            if (doc == null || doc.GetType() != (int)swDocumentTypes_e.swDocPART)
                throw new InvalidOperationException("Ouvrir une pièce mécano-soudée (.sldprt) avant de lancer la commande.");

            double toMm = LengthFactor(doc);
            var result = new SwCutList();
            int item = 0;

            foreach (var folderFeat in CutListFolders(doc))
            {
                var bf = (BodyFolder)folderFeat.GetSpecificFeature2();
                int qty = bf.GetBodyCount();
                if (qty <= 0) continue;
                item++;

                var cpm = folderFeat.CustomPropertyManager;
                var lenTxt = FirstProperty(cpm, _settings.LengthPropertyNames);
                var desc = FirstProperty(cpm, _settings.DescriptionPropertyNames) ?? folderFeat.Name;
                var len = ParseNumber(lenTxt) * toMm;
                var id = ProfileNameMatcher.Match(desc);

                if (len <= 0 || id == null || !ProfileCatalog.TryGet(id, out _))
                {
                    result.Unmatched.Add($"Élément {item} « {folderFeat.Name} » x{qty} : {desc}" + (len > 0 ? $" L={len:0}" : " (tôle ou profil non reconnu)"));
                    continue;
                }
                result.Pieces.Add(new CutPiece { Mark = "R" + item, Role = desc, ProfileId = id, Length = Math.Round(len, 1), Quantity = qty });
            }
            return result;
        }

        private static IEnumerable<Feature> CutListFolders(ModelDoc2 doc)
        {
            // Mise à jour de la liste (sinon longueurs périmées), puis parcours des sous-fonctions.
            var f = (Feature)doc.FirstFeature();
            while (f != null)
            {
                if (f.GetTypeName2() == "SolidBodyFolder")
                {
                    var root = (BodyFolder)f.GetSpecificFeature2();
                    root?.UpdateCutList();
                    var sub = (Feature)f.GetFirstSubFeature();
                    while (sub != null)
                    {
                        if (sub.GetTypeName2() == "CutListFolder") yield return sub;
                        sub = (Feature)sub.GetNextSubFeature();
                    }
                }
                f = (Feature)f.GetNextFeature();
            }
        }

        private static string FirstProperty(CustomPropertyManager cpm, string names)
        {
            if (cpm == null) return null;
            foreach (var n in (names ?? "").Split(';').Select(x => x.Trim()).Where(x => x.Length > 0))
            {
                cpm.Get5(n, false, out string raw, out string resolved, out bool _);
                var v = string.IsNullOrWhiteSpace(resolved) ? raw : resolved;
                if (!string.IsNullOrWhiteSpace(v)) return v;
            }
            return null;
        }

        private static readonly Regex Num = new Regex(@"-?\d+(?:[.,]\d+)?");

        public static double ParseNumber(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            var m = Num.Match(s);
            return m.Success ? double.Parse(m.Value.Replace(',', '.'), CultureInfo.InvariantCulture) : 0;
        }

        /// <summary>Facteur unité du document → mm (les propriétés LENGTH sont exprimées dans l'unité du document).</summary>
        private static double LengthFactor(ModelDoc2 doc)
        {
            try
            {
                var units = (Array)doc.GetUnits();          // [0] = swLengthUnit_e
                switch (Convert.ToInt32(units.GetValue(0)))
                {
                    case (int)swLengthUnit_e.swCM: return 10;
                    case (int)swLengthUnit_e.swMETER: return 1000;
                    case (int)swLengthUnit_e.swINCHES: return 25.4;
                    case (int)swLengthUnit_e.swFEET: return 304.8;
                    default: return 1;
                }
            }
            catch { return 1; }
        }
    }
}
