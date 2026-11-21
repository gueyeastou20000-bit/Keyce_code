using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Keyce_code.App.Views;

namespace Keyce_code.App;

public class MainWindow : Window
{
    public MainWindow()
    {
        Title = "KEYCE_KODE IDE";
        Width = 1000;
        Height = 650;
        Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));

        var activityBar = new ActivityBar();
        var sidebar = new Sidebar();
        activityBar.ViewSelected += id => sidebar.ShowView(id);

        // Temporaire : à remplacer par l'éditeur de ton camarade
        sidebar.Explorer.FileOpenRequested += chemin =>
            MessageBox.Show(chemin, "Fichier à ouvrir");

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Grid.SetColumn(activityBar, 0);
        Grid.SetColumn(sidebar, 1);
        grid.Children.Add(activityBar);
        grid.Children.Add(sidebar);

        Content = grid; // la 3e colonne est réservée à l'éditeur

        // Raccourci Ctrl+B : afficher / cacher la Sidebar
        InputBindings.Add(new KeyBinding(
            new RelayCommand(() => sidebar.Toggle()),
            Key.B, ModifierKeys.Control));
    }
}