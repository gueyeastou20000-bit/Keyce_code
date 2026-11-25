using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KEYCE_KODER.AI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
}
