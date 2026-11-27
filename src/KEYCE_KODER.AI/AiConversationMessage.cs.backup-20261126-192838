namespace KEYCE_KODER.AI;

public sealed class AiConversationMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Role { get; set; } = "user";

    public string Content { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsComplete { get; set; } = true;

    public string? Model { get; set; }
}