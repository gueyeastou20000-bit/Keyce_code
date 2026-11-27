
namespace KEYCE_KODER.AI;

public sealed class AiConversationMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Role { get; set; } = "user";

    // Contenu complet conservé pour reconstruire le contexte de l'IA.
    public string Content { get; set; } = string.Empty;

    // Texte destiné à l'affichage dans le panneau.
    // Null pour les anciens messages ou les messages internes.
    public string? DisplayContent { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsComplete { get; set; } = true;

    public string? Model { get; set; }

    public List<AiChatToolCall>? ToolCalls { get; set; }
}
