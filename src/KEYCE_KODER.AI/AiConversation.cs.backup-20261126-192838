namespace KEYCE_KODER.AI;

public sealed class AiConversation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = "Nouvelle conversation";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string Model { get; set; } = "qwen3.5:latest";

    public string ActiveMode { get; set; } = "Ask";

    public bool ThinkingEnabled { get; set; }

    public int ContextSize { get; set; } = 4096;

    public List<AiConversationMessage> Messages { get; set; } = new();
}