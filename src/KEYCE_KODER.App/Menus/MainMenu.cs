using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KEYCE_KODER.App.Actions;
using KEYCE_KODER.App.Commands;
using KEYCE_KODER.App.Themes;

namespace KEYCE_KODER.App.Menus;

public sealed class MainMenu : Menu
{
    private readonly List<RelayCommand> _commands = new();

    public MenuItem FileMenu { get; }

    public MenuItem EditMenu { get; }

    public MainMenu(FileActions files, EditActions edit, Window window)
    {
        Padding = new Thickness(4);
        Background = ThemeColors.Sidebar;
        Foreground = ThemeColors.TextPrimary;

        // ----- Menu Fichier -----
        FileMenu = CreateMenuItem("_Fichier");

        FileMenu.Items.Add(Item("_Nouveau fichier", "Ctrl+N", files.NewFile));
        FileMenu.Items.Add(Item("_Ouvrir un fichier…", "Ctrl+O", async () => await files.OpenAsync()));
        FileMenu.Items.Add(new Separator());

        FileMenu.Items.Add(Item("_Enregistrer", "Ctrl+S",
            async () => await files.SaveAsync(), () => files.HasActiveDocument));

        FileMenu.Items.Add(Item("Enregistrer _sous…", "Ctrl+Maj+S",
            async () => await files.SaveAsAsync(), () => files.HasActiveDocument));

        FileMenu.Items.Add(Item("_Tout enregistrer", "",
            async () => await files.SaveAllAsync(), () => files.HasUnsavedDocuments));

        FileMenu.Items.Add(new Separator());

        FileMenu.Items.Add(Item("_Fermer l'onglet", "Ctrl+W",
            async () => await files.CloseActiveAsync(), () => files.HasActiveDocument));

        FileMenu.Items.Add(Item("Fermer to_us les onglets", "",
            async () => await files.CloseAllAsync(), () => files.HasActiveDocument));

        FileMenu.Items.Add(new Separator());

        FileMenu.Items.Add(Item("_Quitter", "Alt+F4", window.Close));

        // ----- Menu Édition -----
        EditMenu = CreateMenuItem("_Édition");

        EditMenu.Items.Add(Item("_Annuler", "Ctrl+Z", edit.Undo, () => edit.CanUndo));
        EditMenu.Items.Add(Item("_Rétablir", "Ctrl+Y", edit.Redo, () => edit.CanRedo));

        EditMenu.Items.Add(new Separator());

        EditMenu.Items.Add(Item("Co_uper", "Ctrl+X", edit.Cut, () => edit.HasSelection));
        EditMenu.Items.Add(Item("_Copier", "Ctrl+C", edit.Copy, () => edit.HasSelection));
        EditMenu.Items.Add(Item("C_oller", "Ctrl+V", edit.Paste, () => edit.CanPaste));
        EditMenu.Items.Add(Item("_Supprimer", "Suppr", edit.Delete, () => edit.HasSelection));

        EditMenu.Items.Add(new Separator());

        EditMenu.Items.Add(Item("_Tout sélectionner", "Ctrl+A",
            edit.SelectAll, () => edit.HasEditor));

        Items.Add(FileMenu);
        Items.Add(EditMenu);

        FileMenu.SubmenuOpened += (_, _) => RefreshCommands();
        EditMenu.SubmenuOpened += (_, _) => RefreshCommands();
    }

    private static MenuItem CreateMenuItem(string header)
    {
        return new MenuItem
        {
            Header = header,
            Background = ThemeColors.Sidebar,
            Foreground = ThemeColors.TextPrimary,
            Padding = new Thickness(12, 7, 12, 7),
            FontSize = 13,
            BorderThickness = new Thickness(0)
        };
    }

    public MenuItem Item(string header, string gesture, Action action, Func<bool>? canExecute = null)
    {
        var command = new RelayCommand(action, canExecute);
        _commands.Add(command);

        return new MenuItem
        {
            Header = header,
            InputGestureText = gesture,
            Command = command,
            Background = ThemeColors.Sidebar,
            Foreground = ThemeColors.TextPrimary,
            Padding = new Thickness(12, 7, 12, 7),
            FontSize = 13,
            BorderThickness = new Thickness(0)
        };
    }

    private void RefreshCommands()
    {
        foreach (RelayCommand command in _commands)
            command.NotifyCanExecuteChanged();
    }
}