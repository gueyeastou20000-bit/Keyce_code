using System.Threading.Channels;

namespace KEYCE_KODER.Core.Execution;

public static class UiDispatch
{
    public static void Post(SynchronizationContext? ctx, Action action)
    {
        if (ctx is null) action();
        else ctx.Post(static s => ((Action)s!)(), action);
    }
}

/// <summary>Regroupe les sorties en lots (~30 ms) pour ne pas saturer le thread UI.</summary>
public sealed class OutputPump
{
    private readonly Channel<OutputLine> _channel =
        Channel.CreateUnbounded<OutputLine>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Task _consumer;

    public OutputPump(Action<IReadOnlyList<OutputLine>> sink, SynchronizationContext? ui, int intervalMs = 30)
        => _consumer = Task.Run(async () =>
        {
            var reader = _channel.Reader;
            while (await reader.WaitToReadAsync())
            {
                var batch = new List<OutputLine>();
                while (batch.Count < 5000 && reader.TryRead(out var line)) batch.Add(line);
                if (batch.Count > 0) UiDispatch.Post(ui, () => sink(batch));
                await Task.Delay(intervalMs);
            }
        });

    public void Write(OutputLine line) => _channel.Writer.TryWrite(line);

    /// <summary>Termine l'écriture et attend que tout ait été posté.</summary>
    public Task CompleteAsync()
    {
        _channel.Writer.TryComplete();
        return _consumer;
    }
}
