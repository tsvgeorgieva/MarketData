using System.Text.Json.Serialization;

namespace MarketData.Infrastructure.Binance.Models;

internal sealed class Kline
{
    [JsonPropertyName("s")]
    public required string Symbol { get; init; }

    [JsonPropertyName("i")]
    public required string Interval { get; init; }

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
