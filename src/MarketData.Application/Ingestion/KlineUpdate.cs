using MarketData.Domain.Candles;

namespace MarketData.Application.Ingestion;

/// <summary>One update from Binance for a minute; only a closed one is final and kept.</summary>
public sealed record KlineUpdate(Candle Candle, bool IsClosed);
