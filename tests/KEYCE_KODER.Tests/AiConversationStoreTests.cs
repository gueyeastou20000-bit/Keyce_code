using KEYCE_KODER.AI;

namespace KEYCE_KODER.Tests;

[TestClass]
public sealed class AiConversationStoreTests
{
    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "KEYCE_KODER_TESTS",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);
        return path;
    }

    [TestMethod]
    public async Task SaveAndLoadAsync_ShouldPreserveConversation()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            var store = new AiConversationStore(
                Path.Combine(directory, "conversations.json"));

            var conversation = new AiConversation
            {
                Title = "Test conversation",
                Model = "qwen3.5:latest",
                ActiveMode = "Ask",
                ThinkingEnabled = true,
                ContextSize = 8192
            };

            conversation.Messages.Add(new AiConversationMessage
            {
                Role = "user",
                Content = "Bonjour"
            });

            conversation.Messages.Add(new AiConversationMessage
            {
                Role = "assistant",
                Content = "Bonjour ! Comment puis-je aider ?"
            });

            await store.SaveAsync(new[] { conversation });

            var loaded = await store.LoadAsync();

            Assert.AreEqual(1, loaded.Count);
            Assert.AreEqual("Test conversation", loaded[0].Title);
            Assert.AreEqual("qwen3.5:latest", loaded[0].Model);
            Assert.AreEqual("Ask", loaded[0].ActiveMode);
            Assert.IsTrue(loaded[0].ThinkingEnabled);
            Assert.AreEqual(8192, loaded[0].ContextSize);
            Assert.AreEqual(2, loaded[0].Messages.Count);
            Assert.AreEqual("Bonjour", loaded[0].Messages[0].Content);
            Assert.AreEqual("assistant", loaded[0].Messages[1].Role);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public async Task SaveAndLoadAsync_ShouldPreserveMultipleConversations()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            var store = new AiConversationStore(
                Path.Combine(directory, "conversations.json"));

            var conversations = new List<AiConversation>
            {
                new() { Title = "Première conversation" },
                new() { Title = "Deuxième conversation" },
                new() { Title = "Troisième conversation" }
            };

            await store.SaveAsync(conversations);

            var loaded = await store.LoadAsync();

            Assert.AreEqual(3, loaded.Count);
            Assert.AreEqual("Première conversation", loaded[0].Title);
            Assert.AreEqual("Deuxième conversation", loaded[1].Title);
            Assert.AreEqual("Troisième conversation", loaded[2].Title);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public async Task LoadAsync_WhenFileDoesNotExist_ShouldReturnEmptyList()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            var store = new AiConversationStore(
                Path.Combine(directory, "missing.json"));

            var loaded = await store.LoadAsync();

            Assert.IsNotNull(loaded);
            Assert.AreEqual(0, loaded.Count);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public async Task SaveAsync_WhenListIsEmpty_ShouldSaveEmptyList()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            string filePath = Path.Combine(directory, "conversations.json");
            var store = new AiConversationStore(filePath);

            await store.SaveAsync(Array.Empty<AiConversation>());

            var loaded = await store.LoadAsync();

            Assert.AreEqual(0, loaded.Count);
            Assert.IsTrue(File.Exists(filePath));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public async Task SaveAndLoadAsync_ShouldPreserveIncompleteMessage()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            var store = new AiConversationStore(
                Path.Combine(directory, "conversations.json"));

            var conversation = new AiConversation();

            conversation.Messages.Add(new AiConversationMessage
            {
                Role = "assistant",
                Content = "Réponse interrompue...",
                IsComplete = false,
                Model = "qwen3.5:latest"
            });

            await store.SaveAsync(new[] { conversation });

            var loaded = await store.LoadAsync();

            Assert.AreEqual(1, loaded.Count);
            Assert.AreEqual(1, loaded[0].Messages.Count);
            Assert.IsFalse(loaded[0].Messages[0].IsComplete);
            Assert.AreEqual("qwen3.5:latest", loaded[0].Messages[0].Model);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
