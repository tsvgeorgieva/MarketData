using System.Text.Json.Serialization;

namespace MarketData.Infrastructure.Binance.Models;

/// <summary>A combined-stream message from Binance; the shape mirrors its payload.</summary>
internal sealed class KlineMessage
{
    [JsonPropertyName("data")]
    public required KlineEvent Data { get; init; }
}
