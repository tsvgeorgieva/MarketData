using MarketData.Domain.Candles;

namespace MarketData.Infrastructure.Binance;

/// <summary>One kline message from Binance for a minute; only the closed one is final.</summary>
internal sealed record KlineUpdate(Candle Candle, bool IsClosed);
