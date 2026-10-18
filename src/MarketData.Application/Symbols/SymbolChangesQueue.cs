using MarketData.Domain.Symbols;
using System.Threading.Channels;
using MarketData.Application.Ingestion.Options;
using MarketData.Application.Ingestion;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace MarketData.Application.Symbols;

/// <summary>
/// Carries Symbol changes from the admin service to the Symbol change handler, which applies them to the open Binance connection.
/// Changes only pile up while no connection is open, and a new connection is built from the saved Symbols anyway, so when the queue
/// is full the oldest change is dropped.
/// </summary>
public sealed class SymbolChangesQueue
{
    private readonly Channel<SymbolChange> _changes;

    public SymbolChangesQueue(IOptions<IngestionOptions> options, ILogger<SymbolChangesQueue> logger)
    {
        _changes = Channel.CreateBounded<SymbolChange>(
            new BoundedChannelOptions(options.Value.SymbolChangesQueueCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
            },
            dropped => logger.LogWarning("Symbol change queue full; dropped the {Change} of {Symbol}", dropped.Added ? "add" : "remove", dropped.Symbol));
    }

    public void Publish(SymbolChange change) => _changes.Writer.TryWrite(change);

    public IAsyncEnumerable<SymbolChange> ReadAllAsync(CancellationToken cancellationToken) => _changes.Reader.ReadAllAsync(cancellationToken);
}
