using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using MarketData.Infrastructure.Binance.Models;

namespace MarketData.Infrastructure.Binance;

/// <summary>Reads one combined-stream message and accepts it only when it carries a one-minute kline.</summary>
internal static class KlineMessageParser
{
    // Binance sends prices as JSON strings, so numbers are accepted from strings.
    // Matching of property names stays case-sensitive, which matters because Binance uses both "v" and "V".
    private static readonly JsonSerializerOptions Options = new() { NumberHandling = JsonNumberHandling.AllowReadingFromString };

    public static bool TryParse(ReadOnlyMemory<byte> utf8Json, [NotNullWhen(true)] out KlineMessage? message)
    {
        message = null;
        try
        {
            var parsed = JsonSerializer.Deserialize<KlineMessage>(utf8Json.Span, Options);
            if (parsed is not { Data: { Type: "kline", Kline.Interval: "1m" } })
            {
                return false;
            }

            message = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
