using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MetalForge.Core.Gates;
using MetalForge.Core.Ladders;
using MetalForge.Core.Model;
using MetalForge.Core.Railings;
using MetalForge.Core.Stairs;
using MetalForge.SwAddin.Settings;
using MetalForge.SwAddin.Sw;
using MetalForge.SwAddin.UI;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swpublished;

namespace MetalForge.SwAddin
{
    /// <summary>
    /// Point d'entrée du complément SolidWorks MetalForge (métallerie-serrurerie).
    /// Enregistrement : Install\register.bat (RegAsm /codebase), puis Outils > Compléments.
    /// </summary>
    [ComVisible(true)]
    [Guid("d5a978ef-7a43-42c8-b185-ed0c35334b48")]
    [ProgId("MetalForge.SwAddin")]
    public sealed class MetalForgeAddin : ISwAddin
    {
        public const string Title = "MetalForge";
        private const string Summary = "Métallerie-serrurerie : escaliers, garde-corps, portails, échelles, débit optimisé, devis et notes de calcul";
        private const int MainGroupId = 7351;

        private ISldWorks _sw;
        private ICommandManager _cmdMgr;
        private TaskpaneView _taskPane;
        private TaskPaneControl _paneControl;
        private AddinSettings _settings;

        private static readonly (string Name, string Hint, string Callback, string Enable, IconFactory.Glyph Glyph)[] Commands =
        {
            ("Escalier",      "Escalier droit métallique (Blondel, échappée, limons)", nameof(OnStair),    nameof(EnableAlways), IconFactory.Glyph.Stair),
            ("Garde-corps",   "Garde-corps NF P01-012 droit ou rampant",               nameof(OnRailing),  nameof(EnableAlways), IconFactory.Glyph.Railing),
            ("Portail",       "Portail battant / coulissant / clôture",                nameof(OnGate),     nameof(EnableAlways), IconFactory.Glyph.Gate),
            ("Échelle",       "Échelle fixe à crinoline (NF E85-016 / ISO 14122-4)",   nameof(OnLadder),   nameof(EnableAlways), IconFactory.Glyph.Ladder),
            ("Débit & devis", "Optimisation de débit et chiffrage de la pièce active", nameof(OnCutList),  nameof(EnableIfPart), IconFactory.Glyph.CutList),
            ("Profilés",      "Bibliothèque de profilés",                              nameof(OnProfiles), nameof(EnableAlways), IconFactory.Glyph.Profiles),
            ("Réglages",      "Dossiers de profils, débit, prix",                      nameof(OnSettings), nameof(EnableAlways), IconFactory.Glyph.Settings),
        };

        // ================================================================== ISwAddin

        public bool ConnectToSW(object ThisSW, int Cookie)
        {
            try
            {
                _sw = (ISldWorks)ThisSW;
                _sw.SetAddinCallbackInfo2(0, this, Cookie);
                _settings = AddinSettings.Load();
                AutoDetectProfileFolder();
                _cmdMgr = _sw.GetCommandManager(Cookie);
                CreateCommands();
                CreateTaskPane();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("MetalForge n'a pas pu démarrer : " + ex.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        public bool DisconnectFromSW()
        {
            try
            {
                _cmdMgr?.RemoveCommandGroup(MainGroupId);
                foreach (var t in new[] { (int)swDocumentTypes_e.swDocPART, (int)swDocumentTypes_e.swDocASSEMBLY })
                {
                    var tab = _cmdMgr?.GetCommandTab(t, Title);
                    if (tab != null) _cmdMgr.RemoveCommandTab(tab);
                }
                _taskPane?.DeleteView();
                _paneControl?.Dispose();
            }
            catch { /* fermeture de SolidWorks : on libère quoi qu'il arrive */ }
            finally
            {
                if (_taskPane != null) Marshal.ReleaseComObject(_taskPane);
                if (_cmdMgr != null) Marshal.ReleaseComObject(_cmdMgr);
                if (_sw != null) Marshal.ReleaseComObject(_sw);
                _taskPane = null; _cmdMgr = null; _sw = null;
                GC.Collect(); GC.WaitForPendingFinalizers();
            }
            return true;
        }

        // ================================================================== Interface

        private void CreateCommands()
        {
            int err = 0;
            var grp = _cmdMgr.CreateCommandGroup2(MainGroupId, Title, Summary, Summary, -1, true, ref err);
            grp.IconList = IconFactory.Strips(Commands.Select(c => c.Glyph).ToArray());
            grp.MainIconList = IconFactory.Main();

            var menuTb = (int)(swCommandItemType_e.swMenuItem | swCommandItemType_e.swToolbarItem);
            var indexes = Commands.Select((c, i) => grp.AddCommandItem2(c.Name, -1, c.Hint, c.Name, i, c.Callback, c.Enable, i + 1, menuTb)).ToArray();
            grp.HasToolbar = true;
            grp.HasMenu = true;
            grp.Activate();

            foreach (var docType in new[] { (int)swDocumentTypes_e.swDocPART, (int)swDocumentTypes_e.swDocASSEMBLY })
            {
                var tab = _cmdMgr.GetCommandTab(docType, Title);
                if (tab != null) _cmdMgr.RemoveCommandTab(tab);
                tab = _cmdMgr.AddCommandTab(docType, Title);
                var box = tab.AddCommandTabBox();
                var ids = indexes.Select(i => grp.get_CommandID(i)).ToArray();
                var text = ids.Select(_ => (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow).ToArray();
                box.AddCommands(ids, text);
            }
        }

        private void CreateTaskPane()
        {
            _taskPane = _sw.CreateTaskpaneView2(IconFactory.TaskPaneBitmap(), Title + " — Métallerie");
            _paneControl = new TaskPaneControl(this);
            _paneControl.CreateControl();
            _taskPane.DisplayWindowFromHandlex64(_paneControl.Handle.ToInt64());
        }

        /// <summary>Tente de trouver le dossier des profils mécano-soudés déclaré dans les options SolidWorks.</summary>
        private void AutoDetectProfileFolder()
        {
            if (!string.IsNullOrWhiteSpace(_settings.ProfileLibraryRoot)) return;
            if (Enum.TryParse("swFileLocationsWeldmentProfiles", out swUserPreferenceStringValue_e pref))
            {
                var folders = _sw.GetUserPreferenceStringValue((int)pref);
                var first = folders?.Split(';').FirstOrDefault(f => System.IO.Directory.Exists(f));
                if (first != null) _settings.ProfileLibraryRoot = first;
            }
        }

        // ================================================================== Callbacks (appelés par nom par SolidWorks)

        public int EnableAlways() => 1;

        public int EnableIfPart()
        {
            var doc = (ModelDoc2)_sw.ActiveDoc;
            return doc != null && doc.GetType() == (int)swDocumentTypes_e.swDocPART ? 1 : 0;
        }

        public void OnStair() => Safe(() => RunDesigner(new StairDesigner()));
        public void OnRailing() => Safe(() => RunDesigner(new RailingDesigner()));
        public void OnGate() => Safe(() => RunDesigner(new GateDesigner()));
        public void OnLadder() => Safe(() => RunDesigner(new LadderDesigner()));

        public void OnCutList() => Safe(() =>
        {
            var doc = (ModelDoc2)_sw.ActiveDoc;
            var list = new CutListReader(_settings).Read(doc);
            if (list.Pieces.Count == 0 && list.Unmatched.Count == 0)
            {
                MessageBox.Show(Owner, "Aucune liste des pièces soudées trouvée dans la pièce active.", Title);
                return;
            }
            using (var f = new CutListForm(_settings, list.Pieces, list.Unmatched, doc.GetTitle()))
                f.ShowDialog(Owner);
        });

        public void OnManualCutList() => Safe(() =>
        {
            using (var f = new CutListForm(_settings, null, null, "Saisie libre")) f.ShowDialog(Owner);
        });

        public void OnProfiles() => Safe(() => { using (var f = new ProfilesForm(_settings)) f.ShowDialog(Owner); });

        public void OnSettings() => Safe(() =>
        {
            using (var f = new PropertyForm("MetalForge — Réglages", _settings))
                if (f.ShowDialog(Owner) == DialogResult.OK) { _settings.Save(); _settings.BuildIndex(); }
        });

        private void RunDesigner(IDesigner designer)
        {
            using (var f = new DesignerForm(designer, _settings, Generate))
                f.ShowDialog(Owner);
        }

        private string Generate(StructureModel model)
        {
            var builder = new SwModelBuilder(_sw, _settings);
            builder.Build(model);
            return builder.Warnings.Count == 0 ? null
                 : "Pièce générée avec des remarques :\n\n• " + string.Join("\n• ", builder.Warnings);
        }

        private void Safe(Action a)
        {
            try { a(); }
            catch (Exception ex) { MessageBox.Show(Owner, ex.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private IWin32Window Owner
        {
            get
            {
                try { return new WindowWrapper(new IntPtr(((Frame)_sw.Frame()).GetHWndx64())); }
                catch { return null; }
            }
        }

        private sealed class WindowWrapper : IWin32Window
        {
            public WindowWrapper(IntPtr h) { Handle = h; }
            public IntPtr Handle { get; }
        }

        // ================================================================== Enregistrement COM / SolidWorks

        [ComRegisterFunction]
        public static void RegisterFunction(Type t)
        {
            var guid = "{" + t.GUID.ToString().ToUpperInvariant() + "}";
            using (var hklm = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\SolidWorks\Addins\" + guid))
            {
                hklm.SetValue(null, 1, RegistryValueKind.DWord);
                hklm.SetValue("Description", Summary);
                hklm.SetValue("Title", Title);
            }
            using (var hkcu = Registry.CurrentUser.CreateSubKey(@"Software\SolidWorks\AddInsStartup\" + guid))
                hkcu.SetValue(null, 1, RegistryValueKind.DWord);
        }

        [ComUnregisterFunction]
        public static void UnregisterFunction(Type t)
        {
            var guid = "{" + t.GUID.ToString().ToUpperInvariant() + "}";
            Registry.LocalMachine.DeleteSubKey(@"SOFTWARE\SolidWorks\Addins\" + guid, false);
            Registry.CurrentUser.DeleteSubKey(@"Software\SolidWorks\AddInsStartup\" + guid, false);
        }
    }
}
