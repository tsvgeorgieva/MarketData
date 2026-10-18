namespace MarketData.Domain.Symbols;

/// <summary>A Symbol the system collects; once removed the row is kept with its removal time.</summary>
public sealed class MonitoredSymbol(SymbolCode symbol, DateTimeOffset addedAt)
{
    public SymbolCode Symbol { get; } = symbol;

    public DateTimeOffset AddedAt { get; } = addedAt;

    public DateTimeOffset? RemovedAt { get; private set; }
}
