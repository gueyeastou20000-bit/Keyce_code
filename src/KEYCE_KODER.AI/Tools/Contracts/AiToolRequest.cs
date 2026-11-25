using System.Text.Json;

namespace KEYCE_KODER.AI.Tools.Contracts;

public sealed record AiToolRequest(
    string ToolName,
    JsonElement Arguments
);
