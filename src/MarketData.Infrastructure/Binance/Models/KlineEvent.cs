using System.Text.Json.Serialization;

namespace MarketData.Infrastructure.Binance.Models;

internal sealed class KlineEvent
{
    [JsonPropertyName("e")]
    public required string Type { get; init; }

    [JsonPropertyName("k")]
    public required Kline Kline { get; init; }
}
