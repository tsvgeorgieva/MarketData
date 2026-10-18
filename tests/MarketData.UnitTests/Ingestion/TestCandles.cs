using MarketData.Application.Ingestion;
using MarketData.Domain.Candles;
using MarketData.Domain.Symbols;

namespace MarketData.UnitTests.Ingestion;

internal static class TestCandles
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public static Candle At(int minute, string symbol = "BTCUSDT", decimal close = 100m) =>
        new(SymbolCode.Parse(symbol), Start.AddMinutes(minute), close, close, close, close, 1m);

    public static KlineUpdate Closed(int minute, string symbol = "BTCUSDT") => new(At(minute, symbol), IsClosed: true);

    public static KlineUpdate Open(int minute, string symbol = "BTCUSDT") => new(At(minute, symbol), IsClosed: false);
}
