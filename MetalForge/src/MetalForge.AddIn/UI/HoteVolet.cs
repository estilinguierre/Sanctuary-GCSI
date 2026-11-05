using System.Windows.Forms;
using System.Windows.Forms.Integration;

namespace MetalForge.AddIn.UI
{
    /// <summary>
    /// Contrôle Windows Forms qui héberge la vue WPF dans le panneau des tâches SolidWorks
    /// (SolidWorks attend un handle de fenêtre : DisplayWindowFromHandlex64).
    /// </summary>
    internal sealed class HoteVolet : UserControl
    {
        public HoteVolet(object viewModel)
        {
            var vue = new AccueilView { DataContext = viewModel };
            Controls.Add(new ElementHost { Dock = DockStyle.Fill, Child = vue });
            CreateControl();
        }
    }
}
