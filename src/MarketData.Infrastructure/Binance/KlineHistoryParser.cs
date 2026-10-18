using System.Globalization;
using System.Text.Json;
using MarketData.Domain.Candles;
using MarketData.Domain.Symbols;

namespace MarketData.Infrastructure.Binance;

/// <summary>Reads the REST klines answer: an array of arrays <c>[openTime, open, high, low, close, volume, closeTime, ...]</c> with prices as strings.</summary>
internal static class KlineHistoryParser
{
    public static List<Candle> Parse(JsonElement klines, SymbolCode symbol)
    {
        if (klines.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Binance sent klines that are not an array.");
        }

        return [.. klines.EnumerateArray().Select(kline => ToCandle(kline, symbol))];
    }

    private static Candle ToCandle(JsonElement kline, SymbolCode symbol)
    {
        if (kline.ValueKind != JsonValueKind.Array || kline.GetArrayLength() < 6)
        {
            throw new JsonException("Binance sent a kline with too few fields.");
        }

        return new Candle(
            symbol,
            DateTimeOffset.FromUnixTimeMilliseconds(kline[0].GetInt64()),
            ParsePrice(kline[1]),
            ParsePrice(kline[2]),
            ParsePrice(kline[3]),
            ParsePrice(kline[4]),
            ParsePrice(kline[5]));
    }

    private static decimal ParsePrice(JsonElement value) =>
        decimal.Parse(value.GetString() ?? throw new JsonException("Binance sent a kline number that is not a string."), CultureInfo.InvariantCulture);
}
