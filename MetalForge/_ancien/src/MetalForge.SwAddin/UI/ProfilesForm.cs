using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MetalForge.Core.Profiles;
using MetalForge.SwAddin.Settings;

namespace MetalForge.SwAddin.UI
{
    /// <summary>Bibliothèque de profilés : caractéristiques et fichier SolidWorks associé.</summary>
    public sealed class ProfilesForm : Form
    {
        public ProfilesForm(AddinSettings settings)
        {
            Text = "MetalForge — Bibliothèque de profilés";
            Width = 1000; Height = 650; StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9f); ShowIcon = false;

            var grid = new DataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            grid.Columns.Add("Id", "Identifiant");
            grid.Columns.Add("Des", "Désignation");
            grid.Columns.Add("Fam", "Famille");
            grid.Columns.Add("Kg", "kg/m");
            grid.Columns.Add("M2", "m²/m");
            grid.Columns.Add("Sw", "Fichier SolidWorks (.sldlfp)");
            grid.Columns["Sw"].FillWeight = 260;

            settings.BuildIndex();
            int found = 0;
            foreach (var p in ProfileCatalog.All.OrderBy(p => p.Family).ThenBy(p => p.H).ThenBy(p => p.B).ThenBy(p => p.T))
            {
                var f = settings.ResolveProfileFile(p.Id);
                if (f != null) found++;
                int i = grid.Rows.Add(p.Id, p.Designation, p.Family, p.MassPerMeter.ToString("0.00"), (p.PaintPerimeter / 1000).ToString("0.000"),
                                      f ?? "✗ non trouvé — créer le profil ou l'associer dans Réglages");
                if (f == null) grid.Rows[i].Cells["Sw"].Style.ForeColor = Color.Firebrick;
            }

            var info = new Label
            {
                Dock = DockStyle.Top, Height = 40, Padding = new Padding(6),
                Text = $"{ProfileCatalog.All.Count} profilés au catalogue, {found} associés à un fichier SolidWorks. " +
                       $"Dossier analysé : {(string.IsNullOrEmpty(settings.ProfileLibraryRoot) ? "(non défini — voir Réglages)" : settings.ProfileLibraryRoot)}. " +
                       "Tout identifiant bien formé (ex. TR90x45x3) est aussi accepté par les assistants."
            };
            Controls.Add(grid);
            Controls.Add(info);
        }
    }
}
