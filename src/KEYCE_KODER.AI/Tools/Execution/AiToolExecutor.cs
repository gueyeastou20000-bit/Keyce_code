using KEYCE_KODER.AI.Tools.Contracts;

namespace KEYCE_KODER.AI.Tools.Execution;

public sealed class AiToolExecutor
{
    private readonly AiToolRegistry _registry;

    private readonly Func<AiToolDefinition, CancellationToken, Task<bool>>
        _confirmationHandler;

    public AiToolExecutor(
        AiToolRegistry registry,
        Func<AiToolDefinition, CancellationToken, Task<bool>>
            confirmationHandler)
    {
        _registry = registry ??
            throw new ArgumentNullException(nameof(registry));

        _confirmationHandler = confirmationHandler ??
            throw new ArgumentNullException(nameof(confirmationHandler));
    }

    public async Task<AiToolResult> ExecuteAsync(
        AiToolRequest request,
        AiToolContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        if (!_registry.TryGetTool(request.ToolName, out IAiTool? tool) ||
            tool is null)
        {
            return new AiToolResult(
                false,
                $"Outil inconnu : {request.ToolName}",
                "tool_not_found");
        }

        try
        {
            if (tool.Definition.RequiresConfirmation)
            {
                bool approved = await _confirmationHandler(
                    tool.Definition,
                    cancellationToken);

                if (!approved)
                {
                    return new AiToolResult(
                        false,
                        "L'utilisateur a refusé cette opération.",
                        "confirmation_denied");
                }
            }

            return await tool.ExecuteAsync(
                request.Arguments,
                context,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return new AiToolResult(
                false,
                "L'opération a été annulée.",
                "cancelled");
        }
        catch (UnauthorizedAccessException ex)
        {
            return new AiToolResult(
                false,
                ex.Message,
                "access_denied");
        }
        catch (Exception)
        {
            return new AiToolResult(
                false,
                "Une erreur est survenue pendant l'exécution de l'outil.",
                "execution_failed");
        }
    }
}
