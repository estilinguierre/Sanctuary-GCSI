using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using MetalForge.Core.Costing;
using MetalForge.Core.Export;
using MetalForge.Core.Fabrication;
using MetalForge.Core.Model;
using MetalForge.Core.Profiles;
using MetalForge.SwAddin.Settings;

namespace MetalForge.SwAddin.UI
{
    /// <summary>
    /// Débit optimisé + devis à partir d'une liste de pièces : soit lue dans la pièce SolidWorks active,
    /// soit saisie / collée depuis Excel (Profilé ; Longueur ; Qté ; Repère).
    /// </summary>
    public sealed class CutListForm : Form
    {
        private readonly AddinSettings _settings;
        private readonly DataGridView _grid = new DataGridView
        {
            Dock = DockStyle.Fill, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AllowUserToAddRows = true, RowHeadersWidth = 24
        };
        private readonly TextBox _nesting = DesignerForm.MonoBox();
        private readonly TextBox _cost = DesignerForm.MonoBox();
        private readonly TextBox _offcuts = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9f) };
        private NestingResult _nest;
        private StructureModel _model;
        private readonly string _title;

        public CutListForm(AddinSettings settings, IEnumerable<CutPiece> pieces, IEnumerable<string> unmatched, string title)
        {
            _settings = settings; _title = title ?? "Liste de débit";
            Text = "MetalForge — Débit & devis : " + _title;
            Width = 1150; Height = 720; StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9f); ShowIcon = false;

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Mark", HeaderText = "Repère", FillWeight = 20 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Profile", HeaderText = "Profilé (ex. TC40x40x2)", FillWeight = 35 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Length", HeaderText = "Longueur (mm)", FillWeight = 25 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Qty", HeaderText = "Qté", FillWeight = 12 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Role", HeaderText = "Désignation", FillWeight = 40 });
            foreach (var p in pieces ?? Enumerable.Empty<CutPiece>())
                _grid.Rows.Add(p.Mark, p.ProfileId, p.Length.ToString("0.#", CultureInfo.CurrentCulture), p.Quantity, p.Role);
            _grid.KeyDown += Grid_KeyDown;

            var left = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 420 };
            left.Panel1.Controls.Add(_grid);
            var offPanel = new GroupBox { Text = "Chutes en stock (une par ligne : Profilé ; Longueur)", Dock = DockStyle.Fill };
            offPanel.Controls.Add(_offcuts);
            left.Panel2.Controls.Add(offPanel);

            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(DesignerForm.Page("Plan de débit", _nesting));
            tabs.TabPages.Add(DesignerForm.Page("Devis", _cost));
            if (unmatched != null && unmatched.Any())
            {
                var tb = DesignerForm.MonoBox();
                tb.Text = "Corps non pris en compte dans le débit barres (tôles, profils non reconnus) :\r\n\r\n" + string.Join("\r\n", unmatched)
                        + "\r\n\r\nAstuce : ajoutez la ligne à la main avec un profilé catalogue, ou renseignez la Description du profil.";
                tabs.TabPages.Add(DesignerForm.Page($"Non reconnus ({unmatched.Count()})", tb));
            }

            var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 520 };
            split.Panel1.Controls.Add(left);
            split.Panel2.Controls.Add(tabs);

            var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(4) };
            bar.Controls.Add(DesignerForm.Button("Fermer", (s, e) => Close()));
            bar.Controls.Add(DesignerForm.Button("Optimiser", (s, e) => Run(), bold: true));
            bar.Controls.Add(DesignerForm.Button("Exporter CSV…", (s, e) => Export()));
            bar.Controls.Add(DesignerForm.Button("Réglages débit…", (s, e) => { using (var f = new PropertyForm("Réglages", _settings)) if (f.ShowDialog(this) == DialogResult.OK) { _settings.Save(); Run(); } }));
            bar.Controls.Add(DesignerForm.Button("Hypothèses de prix…", (s, e) => { using (var f = new PropertyForm("Hypothèses de prix", _settings.Cost)) if (f.ShowDialog(this) == DialogResult.OK) { _settings.Save(); Run(); } }));

            Controls.Add(split);
            Controls.Add(bar);
            Load += (s, e) => { split.SplitterDistance = 520; Run(); };
        }

        /// <summary>Coller depuis Excel (Ctrl+V) : colonnes Profilé, Longueur, Qté, Repère.</summary>
        private void Grid_KeyDown(object sender, KeyEventArgs e)
        {
            if (!(e.Control && e.KeyCode == Keys.V) || !Clipboard.ContainsText()) return;
            foreach (var line in Clipboard.GetText().Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                var c = line.Split('\t', ';');
                if (c.Length < 2) continue;
                _grid.Rows.Add(c.Length > 3 ? c[3] : "", c[0], c[1], c.Length > 2 ? c[2] : "1", "");
            }
            e.Handled = true;
        }

        private List<CutPiece> ReadGrid(List<string> errors)
        {
            var list = new List<CutPiece>();
            foreach (DataGridViewRow r in _grid.Rows)
            {
                if (r.IsNewRow) continue;
                var prof = Convert.ToString(r.Cells["Profile"].Value)?.Trim();
                if (string.IsNullOrEmpty(prof)) continue;
                if (!ProfileCatalog.TryGet(prof, out var p)) { errors.Add($"Ligne {r.Index + 1} : profilé « {prof} » inconnu"); continue; }
                if (!TryNum(r.Cells["Length"].Value, out var len) || len <= 0) { errors.Add($"Ligne {r.Index + 1} : longueur invalide"); continue; }
                TryNum(r.Cells["Qty"].Value, out var q);
                list.Add(new CutPiece
                {
                    Mark = Convert.ToString(r.Cells["Mark"].Value) is string m && m.Length > 0 ? m : "L" + (r.Index + 1),
                    ProfileId = p.Id, Length = len, Quantity = Math.Max(1, (int)q), Role = Convert.ToString(r.Cells["Role"].Value) ?? ""
                });
            }
            return list;
        }

        private void Run()
        {
            var errors = new List<string>();
            var pieces = ReadGrid(errors);
            var ns = _settings.ToNestingSettings();
            foreach (var line in _offcuts.Lines)
            {
                var c = line.Split(';', '\t');
                if (c.Length >= 2 && ProfileCatalog.TryGet(c[0].Trim(), out var p) && TryNum(c[1], out var l) && l > 0)
                    ns.ExistingOffcuts.Add((p.Id, l));
            }
            _nest = BarNester.Nest(pieces, ns);

            _model = new StructureModel { Name = _title };
            foreach (var p in pieces)
                for (int i = 0; i < p.Quantity; i++)
                    _model.AddMember("R", p.Role, p.ProfileId, Core.Geometry.Vec3.Zero, new Core.Geometry.Vec3(p.Length, 0, 0));

            _nesting.Text = (errors.Count > 0 ? "⚠ " + string.Join("\r\n⚠ ", errors) + "\r\n\r\n" : "") + _nest.ToReport().Replace("\n", "\r\n");
            _cost.Text = pieces.Count == 0 ? "" : CostEstimator.Estimate(_model, _settings.Cost, _nest).ToReport().Replace("\n", "\r\n")
                         + "\r\n(Tôles non incluses si lues depuis SolidWorks : voir onglet « Non reconnus ».)";
        }

        private void Export()
        {
            if (_nest == null) return;
            using (var d = new SaveFileDialog { Filter = "CSV (Excel)|*.csv", FileName = "debit.csv" })
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    CsvExporter.Save(d.FileName, CsvExporter.Nesting(_nest));
                    if (_model != null) CsvExporter.Save(System.IO.Path.ChangeExtension(d.FileName, null) + "_nomenclature.csv", CsvExporter.BillOfMaterials(_model));
                }
        }

        private static bool TryNum(object v, out double d)
        {
            var s = Convert.ToString(v, CultureInfo.InvariantCulture)?.Trim().Replace(',', '.');
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d);
        }
    }
}
