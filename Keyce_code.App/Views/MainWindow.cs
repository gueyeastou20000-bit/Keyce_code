using Keyce_code.App.Views;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Keyce_code.App;

public class MainWindow : Window
{
    public MainWindow()
    {
        Title = "KEYCE_KODE IDE";
        Width = 1200;
        Height = 750;
        MinWidth = 900;
        MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
        Foreground = Brushes.White;

        // Les trois zones de l'IDE
        var activityBar = new ActivityBar();
        var sidebar = new Sidebar();
        var editor = new EditorView();

        // Clic sur une icône de l'Activity Bar : change la vue de la Sidebar
        activityBar.ViewSelected += id => sidebar.ShowView(id);

        // Clic sur un fichier de l'Explorer : ouverture dans l'éditeur AvalonEdit
        sidebar.Explorer.FileOpenRequested += path => editor.OpenFile(path);

        // Mise en page : Activity Bar | Sidebar | Éditeur
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Grid.SetColumn(activityBar, 0);
        Grid.SetColumn(sidebar, 1);
        Grid.SetColumn(editor, 2);

        grid.Children.Add(activityBar);
        grid.Children.Add(sidebar);
        grid.Children.Add(editor);

        Content = grid;

        // Raccourci Ctrl+B : afficher / cacher la Sidebar
        InputBindings.Add(new KeyBinding(
            new RelayCommand(() => sidebar.Toggle()),
            Key.B, ModifierKeys.Control));
    }
}