using System;
using System.Drawing;
using System.Windows.Forms;
using MetalForge.Core.Costing;
using MetalForge.Core.Export;
using MetalForge.Core.Fabrication;
using MetalForge.Core.Model;
using MetalForge.Core.Reports;
using MetalForge.Core.Stairs;
using MetalForge.SwAddin.Settings;

namespace MetalForge.SwAddin.UI
{
    /// <summary>
    /// Fenêtre générique d'assistant : paramètres (PropertyGrid) à gauche,
    /// aperçu / rapport / débit / devis à droite, recalcul instantané.
    /// </summary>
    public sealed class DesignerForm : Form
    {
        private readonly IDesigner _designer;
        private readonly AddinSettings _settings;
        private readonly Func<StructureModel, string> _generate;
        private readonly PropertyGrid _grid = new PropertyGrid { Dock = DockStyle.Fill, PropertySort = PropertySort.Categorized, ToolbarVisible = false };
        private readonly PreviewControl _preview = new PreviewControl { Dock = DockStyle.Fill };
        private readonly TextBox _report = MonoBox();
        private readonly TextBox _nesting = MonoBox();
        private readonly TextBox _cost = MonoBox();
        private readonly TextBox _galva = MonoBox();
        private CostEstimate _estimate;
        private readonly Label _status = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
        private DesignResult _result;
        private NestingResult _nest;

        /// <param name="generate">Construit le modèle dans SolidWorks ; renvoie un message (avertissements) ou null.</param>
        public DesignerForm(IDesigner designer, AddinSettings settings, Func<StructureModel, string> generate)
        {
            _designer = designer; _settings = settings; _generate = generate;
            Text = "MetalForge — " + designer.Title;
            Width = 1200; Height = 760; StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9f);
            MinimizeBox = false; ShowIcon = false;

            var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 380, FixedPanel = FixedPanel.Panel1 };
            _grid.SelectedObject = designer.Parameters;
            _grid.PropertyValueChanged += (s, e) => { Recompute(); if (designer is Core.Atomic.AtomicDesigner) { _grid.Refresh(); Text = "MetalForge — " + designer.Title; } };
            split.Panel1.Controls.Add(_grid);

            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(Page("Aperçu", _preview));
            tabs.TabPages.Add(Page("Rapport & conformité", _report));
            tabs.TabPages.Add(Page("Plan de débit", _nesting));
            tabs.TabPages.Add(Page("Devis", _cost));
            tabs.TabPages.Add(Page("Galvanisation", _galva));
            split.Panel2.Controls.Add(tabs);

            var bar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(4) };
            bar.Controls.Add(Button("Fermer", (s, e) => Close()));
            bar.Controls.Add(Button("Générer dans SolidWorks", (s, e) => Generate(), bold: true));
            bar.Controls.Add(Button("Note de calcul…", (s, e) => ExportNote()));
            bar.Controls.Add(Button("Exporter DXF…", (s, e) => ExportDxf()));
            bar.Controls.Add(Button("Exporter nomenclature CSV…", (s, e) => ExportCsv()));
            bar.Controls.Add(Button("Hypothèses de prix…", (s, e) => EditCost()));

            var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 44, ColumnCount = 2 };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
            bottom.Controls.Add(_status, 0, 0);
            bottom.Controls.Add(bar, 1, 0);

            Controls.Add(split);
            Controls.Add(bottom);
            Load += (s, e) => { split.SplitterDistance = 380; Recompute(); };
        }

        private void Recompute()
        {
            try
            {
                _result = _designer.Compute();
                _preview.Model = _result.Model;
                _report.Text = _result.ToReport().Replace("\n", "\r\n");
                _nest = BarNester.Nest(CutPiece.FromModel(_result.Model), _settings.ToNestingSettings());
                _nesting.Text = _nest.ToReport().Replace("\n", "\r\n");
                _estimate = CostEstimator.Estimate(_result.Model, _settings.Cost, _nest);
                _cost.Text = _estimate.ToReport().Replace("\n", "\r\n");
                _galva.Text = GalvanizingAdvisor.ToReport(_result.Model).Replace("\n", "\r\n");
                bool ok = _result.IsCompliant;
                _status.ForeColor = ok ? Color.DarkGreen : Color.Firebrick;
                _status.Text = (ok ? "✔ Conforme aux vérifications" : "✖ Non-conformités : voir l'onglet Rapport")
                             + $"   |   {_result.Model.TotalMass:0.0} kg";
            }
            catch (Exception ex)
            {
                _result = null;
                _preview.Model = null;
                _status.ForeColor = Color.Firebrick;
                _status.Text = "Paramètres invalides : " + ex.Message;
            }
        }

        private void Generate()
        {
            if (_result == null) return;
            if (!_result.IsCompliant &&
                MessageBox.Show(this, "L'ouvrage présente des non-conformités. Générer quand même ?", Text,
                                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            Cursor = Cursors.WaitCursor;
            try
            {
                var msg = _generate(_result.Model);
                MessageBox.Show(this, string.IsNullOrEmpty(msg) ? "Pièce mécano-soudée générée." : msg, Text,
                                MessageBoxButtons.OK, string.IsNullOrEmpty(msg) ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Erreur SolidWorks : " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally { Cursor = Cursors.Default; }
        }

        private void ExportCsv()
        {
            if (_result == null) return;
            using (var d = new SaveFileDialog { Filter = "CSV (Excel)|*.csv", FileName = Safe(_result.Model.Name) + ".csv" })
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    CsvExporter.Save(d.FileName, CsvExporter.BillOfMaterials(_result.Model));
                    CsvExporter.Save(System.IO.Path.ChangeExtension(d.FileName, null) + "_debit.csv", CsvExporter.Nesting(_nest));
                }
        }

        private void ExportDxf()
        {
            if (_result == null) return;
            using (var d = new SaveFileDialog { Filter = "DXF R12|*.dxf", FileName = Safe(_result.Model.Name) + ".dxf" })
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    DxfDrawings.FrontView(_result.Model).Save(d.FileName);
                    if (_designer is StairDesigner sd)
                        DxfDrawings.Stringer(sd).Save(System.IO.Path.ChangeExtension(d.FileName, null) + "_limon.dxf");
                    if (_designer is Core.Atomic.AtomicDesigner ad && ad.Last != null && ad.Last.Flats.Count > 0)
                        ad.Last.FlatsDxf().Save(System.IO.Path.ChangeExtension(d.FileName, null) + "_developpes.dxf");
                }
        }

        private bool Galvanized => _settings.Cost.Finish == Finish.Galvanisation || _settings.Cost.Finish == Finish.GalvaThermolaquage;

        /// <summary>Note de calcul HTML autonome (imprimable en PDF), ouverte dans le navigateur.</summary>
        private void ExportNote()
        {
            if (_result == null) return;
            using (var d = new SaveFileDialog { Filter = "Note de calcul HTML|*.html", FileName = Safe(_result.Model.Name) + " - note de calcul.html" })
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    System.IO.File.WriteAllText(d.FileName, CalculationNote.Build(_designer.Title, _result, _nest, _estimate, Galvanized), System.Text.Encoding.UTF8);
                    try { System.Diagnostics.Process.Start(d.FileName); } catch { /* pas de navigateur par défaut */ }
                }
        }

        private void EditCost()
        {
            using (var f = new PropertyForm("Hypothèses de prix", _settings.Cost))
                if (f.ShowDialog(this) == DialogResult.OK) { _settings.Save(); Recompute(); }
        }

        internal static TextBox MonoBox() => new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false,
            Font = new Font("Consolas", 9.5f), BackColor = Color.White
        };

        internal static TabPage Page(string title, Control c) { var p = new TabPage(title); p.Controls.Add(c); return p; }

        internal static Button Button(string text, EventHandler click, bool bold = false)
        {
            var b = new Button { Text = text, AutoSize = true, Height = 30, Margin = new Padding(4, 2, 4, 2) };
            if (bold) b.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            b.Click += click;
            return b;
        }

        private static string Safe(string s)
        {
            foreach (var c in System.IO.Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }
    }

    /// <summary>Petite boîte d'édition d'un objet (réglages, hypothèses de prix).</summary>
    public sealed class PropertyForm : Form
    {
        public PropertyForm(string title, object target)
        {
            Text = title; Width = 560; Height = 640; StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9f); MinimizeBox = false; MaximizeBox = false; ShowIcon = false;
            var grid = new PropertyGrid { Dock = DockStyle.Fill, SelectedObject = target, PropertySort = PropertySort.Categorized };
            var ok = new Button { Text = "Enregistrer", DialogResult = DialogResult.OK, Dock = DockStyle.Right, Width = 120 };
            var cancel = new Button { Text = "Fermer", DialogResult = DialogResult.Cancel, Dock = DockStyle.Right, Width = 100 };
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(4) };
            bottom.Controls.Add(ok); bottom.Controls.Add(cancel);
            Controls.Add(grid); Controls.Add(bottom);
            AcceptButton = ok; CancelButton = cancel;
        }
    }
}
