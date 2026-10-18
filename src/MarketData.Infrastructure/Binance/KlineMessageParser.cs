using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using MarketData.Application.Ingestion;
using MarketData.Domain.Candles;
using MarketData.Domain.Symbols;

namespace MarketData.Infrastructure.Binance;

/// <summary>Reads one combined-stream message carrying a one-minute kline. Property names differ only by case, so they are matched exactly.</summary>
internal static class KlineMessageParser
{
    public static bool TryParse(ReadOnlyMemory<byte> utf8Json, [NotNullWhen(true)] out KlineUpdate? update)
    {
        update = null;
        try
        {
            using var document = JsonDocument.Parse(utf8Json);
            if (!document.RootElement.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("e", out var eventType) || eventType.GetString() != "kline")
            {
                return false;
            }

            var kline = data.GetProperty("k");
            if (kline.GetProperty("i").GetString() != "1m" || !SymbolCode.TryParse(kline.GetProperty("s").GetString(), out var symbol))
            {
                return false;
            }

            var candle = new Candle(
                symbol,
                DateTimeOffset.FromUnixTimeMilliseconds(kline.GetProperty("t").GetInt64()),
                Decimal(kline, "o"),
                Decimal(kline, "h"),
                Decimal(kline, "l"),
                Decimal(kline, "c"),
                Decimal(kline, "v"));
            update = new KlineUpdate(candle, kline.GetProperty("x").GetBoolean());
            return true;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return false;
        }
    }

    private static decimal Decimal(JsonElement element, string name) =>
        decimal.Parse(element.GetProperty(name).GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture);
}
