using System.Text.Json.Serialization;

namespace MarketData.Infrastructure.Binance.Models;

/// <summary>One element of the REST klines answer; Binance sends it as a JSON array, not an object, so a converter reads it.</summary>
[JsonConverter(typeof(KlineHistoryEntryConverter))]
internal sealed record KlineHistoryEntry(long OpenTime, decimal Open, decimal High, decimal Low, decimal Close, decimal Volume);
