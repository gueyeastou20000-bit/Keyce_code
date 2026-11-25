using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KEYCE_KODER.AI;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;
using KEYCE_KODER.AI.Tools.Contracts;

namespace KEYCE_KODER.Tests;

[TestClass]
public class OllamaChatClientTests
{
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }

    [TestMethod]
    public async Task SendAsync_Success_ReturnsAssistantContent()
    {
        var mockHandler = new MockHttpMessageHandler((req, ct) =>
        {
            var json = """
            {
                "model": "qwen3.5:latest",
                "message": {
                    "role": "assistant",
                    "content": "Bonjour ! Je suis KEYCE IA."
                },
                "done": true
            }
            """;

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        });

        using var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("http://localhost:11434/") };
        using var client = new OllamaChatClient(httpClient);

        var messages = new List<AiChatMessage> { new("user", "Bonjour") };
        string result = await client.SendAsync("qwen3.5:latest", messages);

        Assert.AreEqual("Bonjour ! Je suis KEYCE IA.", result);
    }

    [TestMethod]
    public async Task SendChatAsync_WhenDoneReasonIsLength_DetectsTruncatedResponse()
    {
        var mockHandler = new MockHttpMessageHandler((req, ct) =>
        {
            var json = """
            {
                "model": "qwen3.5:latest",
                "message": {
                    "role": "assistant",
                    "content": "Texte partiel..."
                },
                "done": true,
                "done_reason": "length"
            }
            """;

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        });

        using var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("http://localhost:11434/") };
        using var client = new OllamaChatClient(httpClient);

        var messages = new List<AiChatMessage> { new("user", "Prompt") };
        var result = await client.SendChatAsync("qwen3.5:latest", messages);

        Assert.AreEqual("Texte partiel...", result.Content);
        Assert.AreEqual("length", result.DoneReason);
        Assert.IsTrue(result.IsTruncated);
    }

    [TestMethod]
    public async Task SendAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var mockHandler = new MockHttpMessageHandler(async (req, ct) =>
        {
            await Task.Delay(500, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("http://localhost:11434/") };
        using var client = new OllamaChatClient(httpClient);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pré-annulé pour tester l'interruption immédiate

        var messages = new List<AiChatMessage> { new("user", "Test interruption") };

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await client.SendAsync("qwen3.5:latest", messages, cancellationToken: cts.Token);
        });
    }

    [TestMethod]
    public async Task SendAsync_WhenHttpError_ThrowsHttpRequestException()
    {
        var mockHandler = new MockHttpMessageHandler((req, ct) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("model not found", Encoding.UTF8, "text/plain")
            };
            return Task.FromResult(response);
        });

        using var httpClient = new HttpClient(mockHandler) { BaseAddress = new Uri("http://localhost:11434/") };
        using var client = new OllamaChatClient(httpClient);

        var messages = new List<AiChatMessage> { new("user", "Bonjour") };

        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await client.SendAsync("unknown_model", messages);
        });
    }

    [TestMethod]
    public async Task SendAsync_EmptyMessages_ThrowsArgumentException()
    {
        using var client = new OllamaChatClient("http://localhost:11434/");
        var emptyMessages = new List<AiChatMessage>();

        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await client.SendAsync("qwen3.5:latest", emptyMessages);
        });
    }

    [TestMethod]
    public async Task SendChatAsync_WhenModelRequestsTool_ParsesToolCall()
    {
        var mockHandler = new MockHttpMessageHandler((req, ct) =>
        {
            var json = """
        {
            "message": {
                "role": "assistant",
                "content": "",
                "tool_calls": [
                    {
                        "function": {
                            "name": "read_file",
                            "arguments": {
                                "path": "src/Program.cs"
                            }
                        }
                    }
                ]
            },
            "done": true
        }
        """;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        });

        using var httpClient = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("http://localhost:11434/")
        };

        using var client = new OllamaChatClient(httpClient);

        var messages = new List<AiChatMessage>
    {
        new("user", "Lis le fichier Program.cs")
    };

        var result = await client.SendChatAsync("qwen3.5:latest", messages);

        Assert.IsTrue(result.HasToolCalls);
        Assert.AreEqual(1, result.ToolCalls!.Count);
        Assert.AreEqual("read_file", result.ToolCalls[0].Name);

        Assert.AreEqual(
            "src/Program.cs",
            result.ToolCalls[0].Arguments.GetProperty("path").GetString());
    }

    [TestMethod]
    public async Task SendChatAsync_WithTools_SendsToolDefinitionsToOllama()
    {
        string? capturedRequest = null;

        var mockHandler = new MockHttpMessageHandler(async (req, ct) =>
        {
            capturedRequest = await req.Content!.ReadAsStringAsync(ct);

            var json = """
        {
            "message": {
                "role": "assistant",
                "content": "Je peux lire ce fichier."
            },
            "done": true
        }
        """;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("http://localhost:11434/")
        };

        using var client = new OllamaChatClient(httpClient);

        var messages = new List<AiChatMessage>
    {
        new("user", "Lis mon fichier")
    };

        var tools = new List<AiToolDefinition>
    {
        new(
            "read_file",
            "Lit un fichier du projet.",
            false,
            new
            {
                type = "object",
                required = new[] { "path" },
                properties = new
                {
                    path = new
                    {
                        type = "string"
                    }
                }
            })
    };

        await client.SendChatAsync(
            "qwen3.5:latest",
            messages,
            tools: tools);

        Assert.IsNotNull(capturedRequest);

        using var document = JsonDocument.Parse(capturedRequest!);

        var root = document.RootElement;

        Assert.AreEqual(
            "read_file",
            root.GetProperty("tools")[0]
                .GetProperty("function")
                .GetProperty("name")
                .GetString());

        Assert.AreEqual(
            4096,
            root.GetProperty("options")
                .GetProperty("num_ctx")
                .GetInt32());
    }

}
