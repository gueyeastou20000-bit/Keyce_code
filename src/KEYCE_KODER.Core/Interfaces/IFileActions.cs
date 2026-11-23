namespace KEYCE_KODER.Core.Interfaces;

public interface IFileActions
{
    bool HasActiveDocument { get; }

    void NewFile();

    Task OpenAsync();

    Task OpenPathAsync(string filePath);

    Task<bool> SaveAsync();

    Task<bool> SaveAsAsync();

    Task<bool> SaveAllAsync();

    Task<bool> CloseActiveAsync();

    Task<bool> CloseAllAsync();

    Task<bool> ConfirmCloseAllAsync();
}