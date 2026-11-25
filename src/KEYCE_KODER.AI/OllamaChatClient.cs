using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KEYCE_KODER.AI;

public sealed record AiChatMessage(string Role, string Content);

public sealed record AiChatResult(string Content, string? DoneReason = null)
{
    public bool IsTruncated => string.Equals(DoneReason, "length", StringComparison.OrdinalIgnoreCase);
}

public sealed class OllamaChatClient : IDisposable
{
    private readonly HttpClient _httpClient;

    public OllamaChatClient(string endpoint = "http://127.0.0.1:11434/")
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(endpoint),
            Timeout = TimeSpan.FromMinutes(5)
        };
    }

    public OllamaChatClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<string> SendAsync(
        string model,
        IReadOnlyList<AiChatMessage> messages,
        bool thinkingEnabled = false,
        int contextSize = 4096,
        CancellationToken cancellationToken = default)
    {
        var result = await SendChatAsync(model, messages, thinkingEnabled, contextSize, cancellationToken);
        return result.Content;
    }

    public async Task<AiChatResult> SendChatAsync(
        string model,
        IReadOnlyList<AiChatMessage> messages,
        bool thinkingEnabled = false,
        int contextSize = 4096,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("Le nom du modèle est obligatoire.", nameof(model));

        if (messages == null || messages.Count == 0)
            throw new ArgumentException("La conversation est vide.", nameof(messages));

        if (contextSize < 512 || contextSize > 262144)
            throw new ArgumentOutOfRangeException(nameof(contextSize));

        var payload = new
        {
            model,
            messages = messages.Select(m => new
            {
                role = m.Role,
                content = m.Content
            }),
            stream = false,
            think = thinkingEnabled,
            options = new
            {
                num_ctx = contextSize,
                num_predict = 1024
            }
        };

        using var response = await _httpClient.PostAsJsonAsync(
            "api/chat",
            payload,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string error = await response.Content.ReadAsStringAsync(cancellationToken);

            throw new HttpRequestException(
                $"Ollama a retourné une erreur ({(int)response.StatusCode}) : {error}");
        }

        await using var responseStream =
            await response.Content.ReadAsStreamAsync(cancellationToken);

        using var document = await JsonDocument.ParseAsync(
            responseStream,
            cancellationToken: cancellationToken);

        string? doneReason = null;
        if (document.RootElement.TryGetProperty("done_reason", out var doneReasonElement))
        {
            doneReason = doneReasonElement.GetString();
        }

        if (document.RootElement.TryGetProperty("message", out var messageElement) &&
            messageElement.TryGetProperty("content", out var contentElement))
        {
            string? content = contentElement.GetString();

            if (!string.IsNullOrWhiteSpace(content))
                return new AiChatResult(content, doneReason);
        }

        throw new InvalidOperationException(
            "Ollama a répondu, mais aucun contenu exploitable n'a été retourné.");
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
