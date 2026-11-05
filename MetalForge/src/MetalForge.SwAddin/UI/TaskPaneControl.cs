using System;
using System.Drawing;
using System.Windows.Forms;

namespace MetalForge.SwAddin.UI
{
    /// <summary>Volet des tâches MetalForge : accès direct aux assistants (style « palette métier »).</summary>
    public sealed class TaskPaneControl : UserControl
    {
        public TaskPaneControl(MetalForgeAddin addin)
        {
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9f);
            var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(8) };

            layout.Controls.Add(new Label { Text = "MetalForge", Font = new Font("Segoe UI", 14f, FontStyle.Bold), AutoSize = true, ForeColor = Color.FromArgb(45, 75, 120) });
            layout.Controls.Add(new Label { Text = "Métallerie · Serrurerie", AutoSize = true, ForeColor = Color.Gray, Margin = new Padding(3, 0, 3, 12) });

            Section(layout, "Ouvrages paramétriques");
            Add(layout, "Escalier droit", "Blondel, échappée, limons, marches", addin.OnStair);
            Add(layout, "Garde-corps", "NF P01-012:2024, privé / ERP / industriel", addin.OnRailing);
            Add(layout, "Portail / clôture", "Battant, coulissant, panneaux", addin.OnGate);
            Add(layout, "Échelle à crinoline", "NF E85-016, ISO 14122-4", addin.OnLadder);

            Section(layout, "Fabrication");
            Add(layout, "Débit & devis (pièce active)", "Liste des pièces soudées → barres", addin.OnCutList);
            Add(layout, "Débit & devis (saisie libre)", "Coller une liste depuis Excel", addin.OnManualCutList);

            Section(layout, "Outils");
            Add(layout, "Bibliothèque de profilés", "Masses, surfaces, fichiers .sldlfp", addin.OnProfiles);
            Add(layout, "Réglages", "Dossiers, débit, prix", addin.OnSettings);

            Controls.Add(layout);
        }

        private static void Section(Control parent, string title) =>
            parent.Controls.Add(new Label { Text = title.ToUpperInvariant(), AutoSize = true, ForeColor = Color.FromArgb(220, 110, 30), Font = new Font("Segoe UI", 8f, FontStyle.Bold), Margin = new Padding(3, 10, 3, 4) });

        private static void Add(Control parent, string title, string hint, Action action)
        {
            var b = new Button
            {
                Text = title + Environment.NewLine + hint, TextAlign = ContentAlignment.MiddleLeft, Width = 230, Height = 46,
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(244, 246, 250), Margin = new Padding(3, 2, 3, 2)
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(210, 215, 225);
            b.Click += (s, e) => action();
            parent.Controls.Add(b);
        }
    }
}
