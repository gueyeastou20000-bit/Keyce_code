using System.IO;
using KEYCE_KODER.App.Tabs;
using KEYCE_KODER.Core.Interfaces;
using KEYCE_KODER.Core.Services;

namespace KEYCE_KODER.App.Actions;

public sealed class FileActions : IFileActions
{
    private readonly DocumentService _documentService;
    private readonly DocumentManager _documentManager;
    private readonly TabManager _tabs;
    private readonly IDialogService _dialogs;

    public FileActions(
        DocumentService documentService,
        DocumentManager documentManager,
        TabManager tabs,
        IDialogService dialogs)
    {
        _documentService = documentService;
        _documentManager = documentManager;
        _tabs = tabs;
        _dialogs = dialogs;

        // Bouton × ou clic molette sur un onglet → même logique que Ctrl+W
        _tabs.CloseRequested += async (_, tab) => await CloseTabAsync(tab);
    }

    public bool HasActiveDocument => _tabs.ActiveTab != null;

    public bool HasUnsavedDocuments => _tabs.Tabs.Any(t => t.Document.IsDirty);

    // ---------------- Nouveau / Ouvrir ----------------

    public void NewFile()
    {
        var document = _documentService.CreateDocument();
        _tabs.Open(document);
    }

    public async Task OpenAsync()
    {
        string? filePath = _dialogs.AskOpenFilePath();

        if (filePath is null)
            return; // l'utilisateur a annulé

        await OpenPathAsync(filePath);
    }

    public async Task OpenPathAsync(string filePath)
    {
        EditorTab? existing = _tabs.FindByPath(filePath);

        if (existing != null)
        {
            _tabs.Activate(existing);
            return;
        }

        try
        {
            var document = await _documentService.OpenDocumentAsync(filePath);
            _tabs.Open(document);
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(
                "Ouverture impossible",
                $"Impossible d'ouvrir le fichier :\n{filePath}\n\n{ex.Message}");
        }
    }

    // ---------------- Enregistrer ----------------

    public Task<bool> SaveAsync()
    {
        EditorTab? tab = _tabs.ActiveTab;
        return tab is null ? Task.FromResult(false) : SaveTabAsync(tab);
    }

    public Task<bool> SaveAsAsync()
    {
        EditorTab? tab = _tabs.ActiveTab;
        return tab is null ? Task.FromResult(false) : SaveTabAsAsync(tab);
    }

    public async Task<bool> SaveAllAsync()
    {
        foreach (EditorTab tab in _tabs.Tabs.Where(t => t.Document.IsDirty).ToList())
        {
            if (!await SaveTabAsync(tab))
                return false;
        }

        return true;
    }

    private async Task<bool> SaveTabAsync(EditorTab tab)
    {
        // Jamais enregistré : il faut d'abord choisir un nom
        if (string.IsNullOrWhiteSpace(tab.Document.FilePath))
            return await SaveTabAsAsync(tab);

        try
        {
            await _documentService.SaveDocumentAsync(tab.Document);
            tab.RefreshHeader();
            return true;
        }
        catch (Exception ex)
        {
            ShowSaveError(tab.Document.FilePath, ex);
            return false;
        }
    }

    private async Task<bool> SaveTabAsAsync(EditorTab tab)
    {
        string suggestedName = tab.Document.FilePath is null
            ? "sans_titre.txt"
            : Path.GetFileName(tab.Document.FilePath);

        string? filePath = _dialogs.AskSaveFilePath(suggestedName);

        if (filePath is null)
            return false; // l'utilisateur a annulé

        try
        {
            await _documentService.SaveDocumentAsAsync(tab.Document, filePath);

            // L'extension a pu changer (.txt → .py) : on met à jour la coloration
            tab.EditorService.SetLanguage(filePath);
            tab.RefreshHeader();
            return true;
        }
        catch (Exception ex)
        {
            ShowSaveError(filePath, ex);
            return false;
        }
    }

    private void ShowSaveError(string? filePath, Exception ex)
    {
        _dialogs.ShowError(
            "Enregistrement impossible",
            $"Impossible d'enregistrer le fichier :\n{filePath}\n\n{ex.Message}");
    }

    // ---------------- Fermer ----------------

    public Task<bool> CloseActiveAsync()
    {
        EditorTab? tab = _tabs.ActiveTab;
        return tab is null ? Task.FromResult(false) : CloseTabAsync(tab);
    }

    public async Task<bool> CloseTabAsync(EditorTab tab)
    {
        if (!await ConfirmCloseAsync(tab))
            return false;

        _tabs.Remove(tab);
        _documentManager.CloseDocument(tab.Document.Id);
        return true;
    }

    public async Task<bool> CloseAllAsync()
    {
        foreach (EditorTab tab in _tabs.Tabs.ToList())
        {
            if (!await CloseTabAsync(tab))
                return false;
        }

        return true;
    }

    public async Task<bool> ConfirmCloseAllAsync()
    {
        foreach (EditorTab tab in _tabs.Tabs.ToList())
        {
            if (!await ConfirmCloseAsync(tab))
                return false;
        }

        return true;
    }

    private async Task<bool> ConfirmCloseAsync(EditorTab tab)
    {
        if (!tab.Document.IsDirty)
            return true;

        _tabs.Activate(tab); // montre de quel fichier on parle

        return _dialogs.AskSaveChanges(tab.Document.Name) switch
        {
            SaveChangesChoice.Save => await SaveTabAsync(tab),
            SaveChangesChoice.DontSave => true,
            _ => false
        };
    }
}