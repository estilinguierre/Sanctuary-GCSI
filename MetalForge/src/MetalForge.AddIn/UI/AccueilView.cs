using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace MetalForge.AddIn.UI
{
    /// <summary>Vue WPF du volet d'accueil (liée à <see cref="AccueilViewModel"/>).</summary>
    public sealed class AccueilView : UserControl
    {
        public AccueilView()
        {
            var pile = new StackPanel { Margin = new Thickness(12) };
            pile.Children.Add(Texte(nameof(AccueilViewModel.Titre), 18, FontWeights.SemiBold));
            pile.Children.Add(Texte(nameof(AccueilViewModel.VersionMetalForge), 12, FontWeights.Normal));
            var sw = Texte(nameof(AccueilViewModel.VersionSolidWorks), 12, FontWeights.Normal);
            BindingOperations.SetBinding(sw, TextBlock.TextProperty, new Binding(nameof(AccueilViewModel.VersionSolidWorks)) { StringFormat = "SolidWorks {0}" });
            pile.Children.Add(sw);
            pile.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 10) });
            pile.Children.Add(Texte(nameof(AccueilViewModel.Etat), 12, FontWeights.Normal));
            var bouton = new Button { Content = "Ouvrir le journal", Margin = new Thickness(0, 10, 0, 0), Padding = new Thickness(10, 4, 10, 4), HorizontalAlignment = HorizontalAlignment.Left };
            bouton.SetBinding(Button.CommandProperty, new Binding("OuvrirJournalCommand"));
            pile.Children.Add(bouton);
            Content = new ScrollViewer { Content = pile, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Background = Brushes.White;
        }

        private static TextBlock Texte(string chemin, double taille, FontWeight poids)
        {
            var t = new TextBlock { FontSize = taille, FontWeight = poids, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2) };
            t.SetBinding(TextBlock.TextProperty, new Binding(chemin));
            return t;
        }
    }
}
