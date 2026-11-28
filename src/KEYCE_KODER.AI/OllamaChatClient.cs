
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KEYCE_KODER.AI.Tools.Contracts;

namespace KEYCE_KODER.AI;

public sealed record AiChatToolCall(
    string Name,
    JsonElement Arguments
);

public sealed record AiChatMessage(
    string Role,
    string Content,
    IReadOnlyList<AiChatToolCall>? ToolCalls = null
);

public sealed record AiChatResult(
    string Content,
    string? DoneReason = null,
    IReadOnlyList<AiChatToolCall>? ToolCalls = null)
{
    public bool IsTruncated =>
        string.Equals(DoneReason, "length", StringComparison.OrdinalIgnoreCase);

    public bool HasToolCalls => ToolCalls is { Count: > 0 };
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
        CancellationToken cancellationToken = default,
        IReadOnlyList<AiToolDefinition>? tools = null)
    {
        var result = await SendChatAsync(
            model,
            messages,
            thinkingEnabled,
            contextSize,
            cancellationToken,
            tools);

        return result.Content;
    }

    public async Task<AiChatResult> SendChatAsync(
        string model,
        IReadOnlyList<AiChatMessage> messages,
        bool thinkingEnabled = false,
        int contextSize = 4096,
        CancellationToken cancellationToken = default,
        IReadOnlyList<AiToolDefinition>? tools = null)
    {
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("Le nom du modèle est obligatoire.", nameof(model));

        if (messages == null || messages.Count == 0)
            throw new ArgumentException("La conversation est vide.", nameof(messages));

        if (contextSize < 512 || contextSize > 262144)
            throw new ArgumentOutOfRangeException(nameof(contextSize));

        var serializedMessages = messages.Select(message =>
        {
            var item = new Dictionary<string, object?>
            {
                ["role"] = message.Role,
                ["content"] = message.Content
            };

            if (message.ToolCalls is { Count: > 0 })
            {
                item["tool_calls"] = message.ToolCalls.Select(call => new
                {
                    function = new
                    {
                        name = call.Name,
                        arguments = call.Arguments
                    }
                }).ToArray();
            }

            return item;
        }).ToArray();

        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["messages"] = serializedMessages,
            ["stream"] = false,
            ["think"] = thinkingEnabled,
            ["options"] = new
            {
                num_ctx = contextSize,
                num_predict = 1024
            }
        };

        if (tools is { Count: > 0 })
        {
            payload["tools"] = tools.Select(tool => new
            {
                type = "function",
                function = new
                {
                    name = tool.Name,
                    description = tool.Description,
                    parameters = tool.Parameters
                }
            }).ToArray();
        }

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

        var root = document.RootElement;

        string? doneReason = null;

        if (root.TryGetProperty("done_reason", out var doneReasonElement) &&
            doneReasonElement.ValueKind == JsonValueKind.String)
        {
            doneReason = doneReasonElement.GetString();
        }

        string content = string.Empty;
        var toolCalls = new List<AiChatToolCall>();

        if (root.TryGetProperty("message", out var messageElement))
        {
            if (messageElement.TryGetProperty("content", out var contentElement) &&
                contentElement.ValueKind == JsonValueKind.String)
            {
                content = contentElement.GetString() ?? string.Empty;
            }

            if (messageElement.TryGetProperty("tool_calls", out var callsElement) &&
                callsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var callElement in callsElement.EnumerateArray())
                {
                    if (!callElement.TryGetProperty("function", out var functionElement))
                        continue;

                    if (!functionElement.TryGetProperty("name", out var nameElement) ||
                        nameElement.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    string? name = nameElement.GetString();

                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    JsonElement arguments;

                    if (functionElement.TryGetProperty("arguments", out var argumentsElement))
                    {
                        arguments = argumentsElement.Clone();
                    }
                    else
                    {
                        arguments = JsonSerializer.SerializeToElement(
                            new Dictionary<string, object?>());
                    }

                    toolCalls.Add(new AiChatToolCall(name, arguments));
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(content) || toolCalls.Count > 0)
        {
            return new AiChatResult(
                content,
                doneReason,
                toolCalls);
        }

        throw new InvalidOperationException(
            "Ollama a répondu, mais aucun contenu ni appel d'outil exploitable n'a été retourné.");
    }

    /// <summary>
    /// Récupère la liste des modèles installés localement via l'API Ollama (/api/tags).
    /// </summary>
    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("api/tags", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Array.Empty<string>();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (document.RootElement.TryGetProperty("models", out var modelsElement) &&
                modelsElement.ValueKind == JsonValueKind.Array)
            {
                var list = new List<string>();
                foreach (var modelElement in modelsElement.EnumerateArray())
                {
                    if (modelElement.TryGetProperty("name", out var nameElement) &&
                        nameElement.ValueKind == JsonValueKind.String)
                    {
                        var name = nameElement.GetString();
                        if (!string.IsNullOrWhiteSpace(name))
                            list.Add(name);
                    }
                }
                return list;
            }
        }
        catch
        {
            // Retourne une liste vide si Ollama n'est pas accessible
        }

        return Array.Empty<string>();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
