using System.Threading.Channels;

namespace MarketData.Application.Ingestion;

/// <summary>Carries Symbol changes from the admin service to the feed job, which applies them to the open Binance connection.</summary>
public sealed class SymbolChanges
{
    private readonly Channel<SymbolChange> _changes = Channel.CreateUnbounded<SymbolChange>(new UnboundedChannelOptions { SingleReader = true });

    public void Publish(SymbolChange change) => _changes.Writer.TryWrite(change);

    public IAsyncEnumerable<SymbolChange> ReadAllAsync(CancellationToken cancellationToken) => _changes.Reader.ReadAllAsync(cancellationToken);
}
