using System.Text.Json;
using MarketData.Domain.Candles;
using MarketData.Domain.Symbols;
using MarketData.Infrastructure.Binance.Models;

namespace MarketData.Infrastructure.Binance;

/// <summary>Reads the REST klines answer, an array of klines, into Candles of the given Symbol.</summary>
internal static class KlineHistoryParser
{
    public static List<Candle> Parse(ReadOnlyMemory<byte> utf8Json, SymbolCode symbol)
    {
        var entries = JsonSerializer.Deserialize<List<KlineHistoryEntry>>(utf8Json.Span)
            ?? throw new JsonException("Binance sent an empty answer.");

        return [.. entries.Select(entry => new Candle(
            symbol,
            DateTimeOffset.FromUnixTimeMilliseconds(entry.OpenTime),
            entry.Open,
            entry.High,
            entry.Low,
            entry.Close,
            entry.Volume))];
    }
}
