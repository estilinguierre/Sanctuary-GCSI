using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MetalForge.Core;
using MetalForge.Core.Diagnostics;

namespace MetalForge.AddIn.UI
{
    /// <summary>Volet d'accueil (lot L0) : identité, versions, accès au journal.</summary>
    public partial class AccueilViewModel : ObservableObject
    {
        public AccueilViewModel(string versionSolidWorks)
        {
            VersionSolidWorks = versionSolidWorks;
        }

        public string Titre => "Bonjour " + MetalForgeInfo.Nom;
        public string VersionMetalForge => "Version " + MetalForgeInfo.Version;
        public string VersionSolidWorks { get; }
        public string Journal => Core.Diagnostics.Journal.FichierDuJour;

        [ObservableProperty]
        private string _etat = "Prêt. Les modules arrivent avec le lot L1 (escaliers).";

        [RelayCommand]
        private void OuvrirJournal()
        {
            if (!File.Exists(Journal)) Core.Diagnostics.Journal.Info("Ouverture du journal depuis le volet");
            try { Process.Start(new ProcessStartInfo(Journal) { UseShellExecute = true }); }
            catch (System.Exception ex) { Etat = "Impossible d'ouvrir le journal : " + ex.Message; }
        }
    }
}
