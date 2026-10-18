using MarketData.Domain.Symbols;

namespace MarketData.Application.Ingestion;

/// <summary>A Symbol that became monitored (<paramref name="Added"/>) or stopped being monitored, while the service runs.</summary>
public sealed record SymbolChange(SymbolCode Symbol, bool Added);
