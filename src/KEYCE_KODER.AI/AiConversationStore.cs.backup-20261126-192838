using System.Text.Json;

namespace KEYCE_KODER.AI;

public sealed class AiConversationStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public AiConversationStore(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException(
                "Le chemin du fichier est obligatoire.",
                nameof(filePath));

        _filePath = Path.GetFullPath(filePath);
    }

    public async Task<List<AiConversation>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);

        try
        {
            if (!File.Exists(_filePath))
                return new List<AiConversation>();

            await using var stream = new FileStream(
                _filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                useAsync: true);

            return await JsonSerializer.DeserializeAsync<List<AiConversation>>(
                stream,
                JsonOptions,
                cancellationToken) ?? new List<AiConversation>();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(
        IEnumerable<AiConversation> conversations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversations);

        var snapshot = conversations.ToList();

        await _lock.WaitAsync(cancellationToken);

        string temporaryPath = _filePath + ".tmp";

        try
        {
            string? directory = Path.GetDirectoryName(_filePath);

            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                4096,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    snapshot,
                    JsonOptions,
                    cancellationToken);

                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}