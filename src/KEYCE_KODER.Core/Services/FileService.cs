namespace KEYCE_KODER.Core.Services;

public class FileService
{
    public async Task<string> ReadFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        return await File.ReadAllTextAsync(
            filePath,
            cancellationToken
        );
    }

    public async Task SaveFileAsync(
        string filePath,
        string content,
        CancellationToken cancellationToken = default)
    {
        await File.WriteAllTextAsync(
            filePath,
            content,
            cancellationToken
        );
    }
}