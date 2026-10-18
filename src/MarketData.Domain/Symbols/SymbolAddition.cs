namespace MarketData.Domain.Symbols;

/// <summary>The outcome of adding a Symbol: whether it was newly monitored, and its row.</summary>
public sealed record SymbolAddition(bool Added, MonitoredSymbol Monitored);
