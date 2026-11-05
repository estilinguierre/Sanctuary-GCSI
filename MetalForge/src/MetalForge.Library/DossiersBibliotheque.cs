using System;
using System.IO;

namespace MetalForge.Library
{
    /// <summary>
    /// Emplacement de la bibliothèque utilisateur (profils, RAL, modèles d'album).
    /// Partageable sur un serveur (F-GEN-19) en changeant <see cref="Racine"/>.
    /// </summary>
    public static class DossiersBibliotheque
    {
        private static string _racine;

        public static string Racine
        {
            get => _racine ?? (_racine = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "MetalForge", "Bibliotheque"));
            set => _racine = value;
        }

        public static string Profils => Path.Combine(Racine, "Profils");
        public static string Modeles => Path.Combine(Racine, "Modeles");
        public static string Couleurs => Path.Combine(Racine, "Couleurs");
    }
}
