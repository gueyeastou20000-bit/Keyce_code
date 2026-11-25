
using System.Text.Json;
using KEYCE_KODER.AI.Tools.Contracts;
using KEYCE_KODER.AI.Tools.Execution;

namespace KEYCE_KODER.Tests;

[TestClass]
public sealed class AiToolExecutorTests
{
    private static AiToolRequest CreateRequest()
    {
        using JsonDocument document = JsonDocument.Parse(
            """{"path":"test.txt","content":"Bonjour"}""");

        return new AiToolRequest(
            "test_tool",
            document.RootElement.Clone());
    }

    private static AiToolContext CreateContext()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "KEYCE_KODER_TESTS",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);
        return new AiToolContext(directory);
    }

    private sealed class FakeTool : IAiTool
    {
        public AiToolDefinition Definition { get; }

        public int ExecutionCount { get; private set; }

        public FakeTool(bool requiresConfirmation)
        {
            Definition = new AiToolDefinition(
                "test_tool",
                "Outil de test",
                requiresConfirmation,
                new { type = "object" });
        }

        public Task<AiToolResult> ExecuteAsync(
            JsonElement arguments,
            AiToolContext context,
            CancellationToken cancellationToken = default)
        {
            ExecutionCount++;

            return Task.FromResult(
                new AiToolResult(true, "Exécution réussie."));
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldRejectUnknownTool()
    {
        var registry = new AiToolRegistry();

        var executor = new AiToolExecutor(
            registry,
            (_, _, _) => Task.FromResult(true));

        AiToolResult result = await executor.ExecuteAsync(
            CreateRequest(),
            CreateContext());

        Assert.IsFalse(result.Success);
        Assert.AreEqual("tool_not_found", result.ErrorCode);
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldNotExecuteWhenConfirmationIsDenied()
    {
        var registry = new AiToolRegistry();
        var tool = new FakeTool(true);
        registry.Register(tool);

        var executor = new AiToolExecutor(
            registry,
            (_, _, _) => Task.FromResult(false));

        AiToolResult result = await executor.ExecuteAsync(
            CreateRequest(),
            CreateContext());

        Assert.IsFalse(result.Success);
        Assert.AreEqual("confirmation_denied", result.ErrorCode);
        Assert.AreEqual(0, tool.ExecutionCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldExecuteWhenConfirmationIsApproved()
    {
        var registry = new AiToolRegistry();
        var tool = new FakeTool(true);
        registry.Register(tool);

        var executor = new AiToolExecutor(
            registry,
            (_, _, _) => Task.FromResult(true));

        AiToolResult result = await executor.ExecuteAsync(
            CreateRequest(),
            CreateContext());

        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, tool.ExecutionCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldNotAskConfirmationWhenNotRequired()
    {
        var registry = new AiToolRegistry();
        var tool = new FakeTool(false);
        registry.Register(tool);

        bool confirmationCalled = false;

        var executor = new AiToolExecutor(
            registry,
            (_, _, _) =>
            {
                confirmationCalled = true;
                return Task.FromResult(false);
            });

        AiToolResult result = await executor.ExecuteAsync(
            CreateRequest(),
            CreateContext());

        Assert.IsTrue(result.Success);
        Assert.IsFalse(confirmationCalled);
        Assert.AreEqual(1, tool.ExecutionCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldPassExactRequestToConfirmationHandler()
    {
        var registry = new AiToolRegistry();
        var tool = new FakeTool(true);
        registry.Register(tool);

        AiToolRequest request = CreateRequest();
        AiToolRequest? receivedRequest = null;

        var executor = new AiToolExecutor(
            registry,
            (_, received, _) =>
            {
                receivedRequest = received;
                return Task.FromResult(false);
            });

        await executor.ExecuteAsync(request, CreateContext());

        Assert.AreSame(request, receivedRequest);
    }

    [TestMethod]
    public async Task ExecuteAsync_ShouldReturnCancelledWhenTokenIsCancelled()
    {
        var registry = new AiToolRegistry();
        var tool = new FakeTool(false);
        registry.Register(tool);

        var executor = new AiToolExecutor(
            registry,
            (_, _, _) => Task.FromResult(true));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        AiToolResult result = await executor.ExecuteAsync(
            CreateRequest(),
            CreateContext(),
            cancellation.Token);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("cancelled", result.ErrorCode);
        Assert.AreEqual(0, tool.ExecutionCount);
    }
}
