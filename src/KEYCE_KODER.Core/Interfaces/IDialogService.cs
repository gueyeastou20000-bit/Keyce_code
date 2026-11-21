namespace KEYCE_KODER.Core.Interfaces;

public enum SaveChangesChoice
{
    Save,
    Discard,
    Cancel  
}

public interface IDialogService
{
    string? AskOpenFilePath();

    string? AskSaveFilePath(string suggestedFileName);

    SaveChangesChoice AskSaveChanges(string documentName);

    void ShowError(string title, string message);

    void ShowInfo(string title, string message);
}