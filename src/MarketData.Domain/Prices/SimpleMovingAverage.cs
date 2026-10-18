using MarketData.Domain.Symbols;

namespace MarketData.Domain.Prices;

/// <summary>
/// The mean Price of the Intervals that exist among the <paramref name="Requested"/> ones ending at the Reference Instant, with the window they cover.
/// <paramref name="InProgress"/> is true when the newest Interval is still forming.
/// </summary>
public sealed record SimpleMovingAverage(
    SymbolCode Symbol,
    Period Period,
    int Requested,
    DateTime From,
    DateTime To,
    decimal Average,
    int DataPoints,
    bool InProgress);
