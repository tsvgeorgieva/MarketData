using MarketData.Domain.Symbols;

namespace MarketData.Domain.Prices;

/// <summary>The mean Price of a Symbol's Candles, with the window they actually cover (from the first one opening to the last one closing) and how many there were.</summary>
public sealed record AveragePrice(SymbolCode Symbol, DateTimeOffset From, DateTimeOffset To, decimal Average, int DataPoints);
