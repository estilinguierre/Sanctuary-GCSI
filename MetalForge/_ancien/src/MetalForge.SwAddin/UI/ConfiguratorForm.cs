using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using MetalForge.Core.Atomic;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace MetalForge.SwAddin.UI
{
    /// <summary>
    /// Configurateur atomique : la page web (web\configurateur) affichée dans SolidWorks via WebView2.
    /// La page envoie le modèle calculé ({ action: "build", result }) ; le complément le génère en pièce mécano-soudée.
    /// </summary>
    public sealed class ConfiguratorForm : Form
    {
        private readonly WebView2 _web = new WebView2 { Dock = DockStyle.Fill };
        private readonly ConfiguratorEngine _engine;
        private readonly Func<AtomicResult, string> _generate;
        private readonly string _folder;

        public static string WebFolder => Path.Combine(Path.GetDirectoryName(typeof(ConfiguratorForm).Assembly.Location), "web", "configurateur");

        /// <summary>Vrai si le runtime Microsoft Edge WebView2 est installé (Windows 10/11 : en général oui).</summary>
        public static bool RuntimeAvailable
        {
            get { try { return !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString()); } catch { return false; } }
        }

        public ConfiguratorForm(ConfiguratorEngine engine, Func<AtomicResult, string> generate)
        {
            _engine = engine; _generate = generate; _folder = WebFolder;
            Text = "MetalForge — Configurateur atomique";
            Width = 1440; Height = 900; StartPosition = FormStartPosition.CenterParent; ShowIcon = false;
            Font = new Font("Segoe UI", 9f);
            Controls.Add(_web);
            Load += async (s, e) =>
            {
                try
                {
                    var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MetalForge", "WebView2");
                    var env = await CoreWebView2Environment.CreateAsync(null, data);
                    await _web.EnsureCoreWebView2Async(env);
                    _web.CoreWebView2.SetVirtualHostNameToFolderMapping("metalforge.local", _folder, CoreWebView2HostResourceAccessKind.Allow);
                    _web.CoreWebView2.WebMessageReceived += OnMessage;
                    _web.Source = new Uri("https://metalforge.local/index.html");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Impossible d'afficher le configurateur : " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Close();
                }
            };
        }

        /// <summary>Charge une configuration enregistrée ({ family, params }).</summary>
        public void LoadConfiguration(string family, string paramsJson) =>
            _web.CoreWebView2?.PostWebMessageAsString("{\"action\":\"load\",\"family\":\"" + family + "\",\"params\":" + (paramsJson ?? "{}") + "}");

        private void OnMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string text;
            try { text = e.TryGetWebMessageAsString(); } catch { text = e.WebMessageAsJson; }
            try
            {
                var msg = (IDictionary<string, object>)_engine.ParseJson(text);
                var action = msg.TryGetValue("action", out var a) ? a as string : null;
                if (action == "build")
                {
                    var root = (IDictionary<string, object>)msg["result"];
                    var res = AtomicResult.FromObject(root, _engine.ToJson(root));
                    Cursor = Cursors.WaitCursor;
                    var warn = _generate(res);
                    Cursor = Cursors.Default;
                    MessageBox.Show(this, string.IsNullOrEmpty(warn) ? "Pièce mécano-soudée générée : " + res.Model.Name : warn, Text,
                                    MessageBoxButtons.OK, string.IsNullOrEmpty(warn) ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                }
                else if (action == "save")
                {
                    var kind = msg["kind"] as string; var name = msg["name"] as string; var content = msg["content"] as string;
                    using (var d = new SaveFileDialog { FileName = Safe(name), Filter = kind == "dxf" ? "DXF|*.dxf" : "CSV (Excel)|*.csv" })
                        if (d.ShowDialog(this) == DialogResult.OK)
                            File.WriteAllText(d.FileName, content, kind == "csv" ? new System.Text.UTF8Encoding(true) : (System.Text.Encoding)System.Text.Encoding.ASCII);
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show(this, "Erreur : " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static string Safe(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) s = (s ?? "ouvrage").Replace(c, '_');
            return s;
        }
    }
}
