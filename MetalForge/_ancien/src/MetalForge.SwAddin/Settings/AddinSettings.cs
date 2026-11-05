using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using MetalForge.Core.Costing;
using MetalForge.Core.Fabrication;
using MetalForge.Core.Profiles;

namespace MetalForge.SwAddin.Settings
{
    /// <summary>Association manuelle profilé catalogue → fichier de profil SolidWorks (.sldlfp).</summary>
    public sealed class ProfileMapEntry
    {
        [DisplayName("Profilé (ex. TC40x40x2)")] public string ProfileId { get; set; }
        [DisplayName("Fichier .sldlfp")] public string File { get; set; }
        public override string ToString() => $"{ProfileId} → {Path.GetFileName(File ?? "")}";
    }

    /// <summary>Réglages persistés dans %APPDATA%\MetalForge\settings.xml.</summary>
    public sealed class AddinSettings
    {
        [Category("Profils SolidWorks"), DisplayName("Dossier des profils mécano-soudés"),
         Description("Racine contenant les fichiers .sldlfp (ex. ...\\weldment profiles). Recherche récursive.")]
        public string ProfileLibraryRoot { get; set; } = "";

        [Category("Profils SolidWorks"), DisplayName("Associations manuelles"),
         Description("Prioritaires sur la recherche automatique. Utile pour les profils perso (plats, carrés pleins...).")]
        public List<ProfileMapEntry> ProfileMap { get; set; } = new List<ProfileMapEntry>();

        [Category("Profils SolidWorks"), DisplayName("Gabarit de pièce"), Description("Vide = gabarit par défaut SolidWorks.")]
        public string PartTemplate { get; set; } = "";

        [Category("Configurateur"), DisplayName("Générer la maçonnerie"), Description("Crée une pièce séparée « maçonnerie » (dalles, murs, piliers) pour l'implantation et le rendu.")]
        public bool BuildMasonry { get; set; } = true;

        [Category("Liste des pièces soudées"), DisplayName("Propriétés longueur"), Description("Noms séparés par « ; ».")]
        public string LengthPropertyNames { get; set; } = "LENGTH;LONGUEUR;Length;Longueur";

        [Category("Liste des pièces soudées"), DisplayName("Propriétés description")]
        public string DescriptionPropertyNames { get; set; } = "Description;DESCRIPTION;Désignation";

        [Category("Débit"), DisplayName("Longueur commerciale (mm)")] public double StockLength { get; set; } = 6000;
        [Category("Débit"), DisplayName("Trait de scie (mm)")] public double Kerf { get; set; } = 3;
        [Category("Débit"), DisplayName("Équerrage début de barre (mm)")] public double EndTrim { get; set; } = 10;
        [Category("Débit"), DisplayName("Chute réutilisable mini (mm)")] public double MinReusableOffcut { get; set; } = 500;

        [Category("Chiffrage"), DisplayName("Hypothèses de prix"), TypeConverter(typeof(ExpandableObjectConverter))]
        public CostParameters Cost { get; set; } = new CostParameters();

        public NestingSettings ToNestingSettings() => new NestingSettings
        {
            StockLength = StockLength, Kerf = Kerf, EndTrim = EndTrim, MinReusableOffcut = MinReusableOffcut
        };

        // ------------------------------------------------------------------ persistance

        public static string FilePath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MetalForge", "settings.xml");

        public static AddinSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    using (var fs = File.OpenRead(FilePath))
                        return (AddinSettings)new XmlSerializer(typeof(AddinSettings)).Deserialize(fs);
            }
            catch { /* fichier corrompu : on repart des valeurs par défaut */ }
            return new AddinSettings();
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            using (var fs = File.Create(FilePath))
                new XmlSerializer(typeof(AddinSettings)).Serialize(fs, this);
        }

        // ------------------------------------------------------------------ résolution des profils

        private Dictionary<string, string> _index;
        private string _indexedRoot;

        /// <summary>Retourne le chemin .sldlfp pour un profilé catalogue, ou null s'il est introuvable.</summary>
        public string ResolveProfileFile(string profileId)
        {
            var manual = ProfileMap.FirstOrDefault(e => string.Equals(e.ProfileId, profileId, StringComparison.OrdinalIgnoreCase));
            if (manual != null && File.Exists(manual.File)) return manual.File;

            if (_index == null || _indexedRoot != ProfileLibraryRoot) BuildIndex();
            return _index.TryGetValue(profileId, out var f) ? f : null;
        }

        /// <summary>Indexe tous les .sldlfp du dossier : « square tube\40 x 40 x 4.sldlfp » → TC40x40x4.</summary>
        public void BuildIndex()
        {
            _index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _indexedRoot = ProfileLibraryRoot;
            if (string.IsNullOrWhiteSpace(ProfileLibraryRoot) || !Directory.Exists(ProfileLibraryRoot)) return;
            foreach (var file in Directory.EnumerateFiles(ProfileLibraryRoot, "*.sldlfp", SearchOption.AllDirectories))
            {
                var folder = Path.GetFileName(Path.GetDirectoryName(file));
                var id = ProfileNameMatcher.Match(folder + " " + Path.GetFileNameWithoutExtension(file));
                if (id != null && !_index.ContainsKey(id)) _index[id] = file;
            }
        }

        [XmlIgnore, Category("Profils SolidWorks"), DisplayName("Profils reconnus dans le dossier")]
        public int IndexedCount { get { if (_index == null || _indexedRoot != ProfileLibraryRoot) BuildIndex(); return _index.Count; } }
    }
}
