using System;
using System.Runtime.InteropServices;
using MetalForge.AddIn.UI;
using MetalForge.Core;
using MetalForge.Core.Diagnostics;
using MetalForge.SwAdapter;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swpublished;

namespace MetalForge.AddIn
{
    /// <summary>
    /// Complément MetalForge pour SolidWorks 2022+ (F-GEN-03, lot L0).
    /// Visible dans Outils &gt; Compléments ; ouvre le volet MetalForge dans le panneau des tâches.
    /// </summary>
    [ComVisible(true)]
    [Guid("a6fba09a-5265-40fd-8d1c-7e766f8b72eb")]
    [ProgId("MetalForge.AddIn")]
    public sealed class MetalForgeAddIn : ISwAddin
    {
        public const string Titre = "MetalForge";
        public const string Description = "Métallerie, serrurerie : escaliers, garde-corps, portails, développés, débits";

        private ISldWorks _sw;
        private SwSession _session;
        private TaskpaneView _volet;
        private HoteVolet _hote;

        public bool ConnectToSW(object ThisSW, int Cookie)
        {
            try
            {
                _sw = (ISldWorks)ThisSW;
                _sw.SetAddinCallbackInfo2(0, this, Cookie);
                _session = new SwSession(_sw);
                Journal.Info($"Connexion {MetalForgeInfo.Nom} {MetalForgeInfo.Version} — SolidWorks {_session.VersionSolidWorks}");
                CreerVolet();
                return true;
            }
            catch (Exception ex)
            {
                Journal.Erreur("Connexion à SolidWorks impossible", ex);
                return false;
            }
        }

        public bool DisconnectFromSW()
        {
            try
            {
                _volet?.DeleteView();
                _hote?.Dispose();
                Journal.Info("Déconnexion de SolidWorks");
            }
            catch (Exception ex) { Journal.Erreur("Déconnexion", ex); }
            finally
            {
                if (_volet != null) Marshal.ReleaseComObject(_volet);
                _volet = null; _hote = null; _session = null; _sw = null;
                GC.Collect(); GC.WaitForPendingFinalizers();
            }
            return true;
        }

        private void CreerVolet()
        {
            _volet = _sw.CreateTaskpaneView2(Icones.VoletBmp(), Titre + " — Métallerie");
            _hote = new HoteVolet(new AccueilViewModel(_session.VersionSolidWorks));
            _volet.DisplayWindowFromHandlex64(_hote.Handle.ToInt64());
        }

        // ------------------------------------------------------------ enregistrement COM (RegAsm /codebase)

        [ComRegisterFunction]
        public static void Enregistrer(Type t)
        {
            var cle = "{" + t.GUID.ToString().ToUpperInvariant() + "}";
            using (var hklm = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\SolidWorks\Addins\" + cle))
            {
                hklm.SetValue(null, 1, RegistryValueKind.DWord);
                hklm.SetValue("Title", Titre);
                hklm.SetValue("Description", Description);
            }
            using (var hkcu = Registry.CurrentUser.CreateSubKey(@"Software\SolidWorks\AddInsStartup\" + cle))
                hkcu.SetValue(null, 1, RegistryValueKind.DWord); // chargé au démarrage
        }

        [ComUnregisterFunction]
        public static void Desinscrire(Type t)
        {
            var cle = "{" + t.GUID.ToString().ToUpperInvariant() + "}";
            Registry.LocalMachine.DeleteSubKey(@"SOFTWARE\SolidWorks\Addins\" + cle, false);
            Registry.CurrentUser.DeleteSubKey(@"Software\SolidWorks\AddInsStartup\" + cle, false);
        }
    }
}
