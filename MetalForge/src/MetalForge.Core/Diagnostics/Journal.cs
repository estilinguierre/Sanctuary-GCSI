using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace MetalForge.Core.Diagnostics
{
    public enum NiveauJournal { Info, Attention, Erreur }

    /// <summary>
    /// Journal de fonctionnement (F-GEN-03, lot L0) : un fichier texte par jour,
    /// dans %LOCALAPPDATA%\MetalForge\journal par défaut. Ne lève jamais d'exception.
    /// </summary>
    public static class Journal
    {
        private static readonly object Verrou = new object();
        private static string _dossier = DossierParDefaut();

        public static string Dossier
        {
            get => _dossier;
            set => _dossier = string.IsNullOrWhiteSpace(value) ? DossierParDefaut() : value;
        }

        /// <summary>Fichier du jour : metalforge-AAAAMMJJ.log.</summary>
        public static string FichierDuJour => Path.Combine(Dossier, "metalforge-" + DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log");

        public static void Info(string message) => Ecrire(NiveauJournal.Info, message, null);
        public static void Attention(string message) => Ecrire(NiveauJournal.Attention, message, null);
        public static void Erreur(string message, Exception ex = null) => Ecrire(NiveauJournal.Erreur, message, ex);

        public static void Ecrire(NiveauJournal niveau, string message, Exception ex)
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
                  .Append(" [").Append(Libelle(niveau)).Append("] ").Append(message);
                if (ex != null) sb.AppendLine().Append(ex);
                lock (Verrou)
                {
                    Directory.CreateDirectory(Dossier);
                    File.AppendAllText(FichierDuJour, sb.AppendLine().ToString(), Encoding.UTF8);
                }
            }
            catch
            {
                // Le journal ne doit jamais faire échouer SolidWorks.
            }
        }

        private static string Libelle(NiveauJournal n) => n == NiveauJournal.Info ? "INFO" : n == NiveauJournal.Attention ? "ATTENTION" : "ERREUR";

        private static string DossierParDefaut()
        {
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(baseDir)) baseDir = Path.GetTempPath();
            return Path.Combine(baseDir, "MetalForge", "journal");
        }
    }
}
