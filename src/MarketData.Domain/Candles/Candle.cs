using MarketData.Domain.Symbols;

namespace MarketData.Domain.Candles;

/// <summary>A closed one-minute summary of a Symbol's trading, identified by Symbol and open time.</summary>
public sealed record Candle(
    SymbolCode Symbol,
    DateTimeOffset OpenTime,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume)
{
    public static readonly TimeSpan Length = TimeSpan.FromMinutes(1);
}
