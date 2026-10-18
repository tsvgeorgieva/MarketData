using MarketData.Application.Ingestion;
using MarketData.Domain.Symbols;

namespace MarketData.UnitTests.Ingestion;

/// <summary>Plays a fixed list of updates, then ends or fails, and records how it was used.</summary>
internal sealed class ScriptedBinanceFeed(IEnumerable<KlineUpdate> updates, Exception? thenFailWith = null)
    : IBinanceStreamFactory, IBinanceStream
{
    private readonly TaskCompletionSource _exhausted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int Connects { get; private set; }

    public IReadOnlyCollection<SymbolCode>? ConnectedWith { get; private set; }

    /// <summary>Completes once every scripted update has been read.</summary>
    public Task Exhausted => _exhausted.Task;

    public Task<IBinanceStream> ConnectAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken)
    {
        Connects++;
        ConnectedWith = symbols;
        return Task.FromResult<IBinanceStream>(this);
    }

    public async IAsyncEnumerable<KlineUpdate> ReadAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();
        foreach (var update in updates)
        {
            yield return update;
        }

        _exhausted.TrySetResult();
        if (thenFailWith is not null)
        {
            throw thenFailWith;
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
