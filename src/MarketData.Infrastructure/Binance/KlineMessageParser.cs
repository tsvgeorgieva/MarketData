using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using MarketData.Domain.Candles;
using MarketData.Domain.Symbols;

namespace MarketData.Infrastructure.Binance;

/// <summary>Reads one combined-stream message carrying a one-minute kline.</summary>
internal static class KlineMessageParser
{
    // Binance sends prices as JSON strings, so numbers are accepted from strings.
    // Matching of property names stays case-sensitive, which matters because Binance uses both "v" and "V".
    private static readonly JsonSerializerOptions Options = new() { NumberHandling = JsonNumberHandling.AllowReadingFromString };

    public static bool TryParse(ReadOnlyMemory<byte> utf8Json, [NotNullWhen(true)] out KlineUpdate? update)
    {
        update = null;
        try
        {
            var message = JsonSerializer.Deserialize<Message>(utf8Json.Span, Options);
            if (message?.Data is not { Type: "kline", Kline: { Interval: "1m" } kline } ||
                !SymbolCode.TryParse(kline.Symbol, out var symbol))
            {
                return false;
            }

            var candle = new Candle(
                symbol,
                DateTimeOffset.FromUnixTimeMilliseconds(kline.OpenTime),
                kline.Open,
                kline.High,
                kline.Low,
                kline.Close,
                kline.Volume);
            update = new KlineUpdate(candle, kline.IsClosed);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    // The shapes below mirror Binance's combined-stream payload.
    private sealed class Message
    {
        [JsonPropertyName("data")]
        public Event? Data { get; init; }
    }

    private sealed class Event
    {
        [JsonPropertyName("e")]
        public string? Type { get; init; }

        [JsonPropertyName("k")]
        public Kline? Kline { get; init; }
    }

    private sealed class Kline
    {
        [JsonPropertyName("s")]
        public string? Symbol { get; init; }

        [JsonPropertyName("i")]
        public string? Interval { get; init; }

        [JsonPropertyName("t")]
        public required long OpenTime { get; init; }

        [JsonPropertyName("o")]
        public required decimal Open { get; init; }

        [JsonPropertyName("h")]
        public required decimal High { get; init; }

        [JsonPropertyName("l")]
        public required decimal Low { get; init; }

        [JsonPropertyName("c")]
        public required decimal Close { get; init; }

        [JsonPropertyName("v")]
        public required decimal Volume { get; init; }

        [JsonPropertyName("x")]
        public required bool IsClosed { get; init; }
    }
}
