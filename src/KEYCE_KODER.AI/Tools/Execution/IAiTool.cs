using System.Text.Json;
using KEYCE_KODER.AI.Tools.Contracts;

namespace KEYCE_KODER.AI.Tools.Execution;

public interface IAiTool
{
    AiToolDefinition Definition { get; }

    Task<AiToolResult> ExecuteAsync(
        JsonElement arguments,
        AiToolContext context,
        CancellationToken cancellationToken = default);
}
