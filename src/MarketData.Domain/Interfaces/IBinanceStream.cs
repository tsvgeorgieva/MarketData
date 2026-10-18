using MarketData.Domain.Candles;

namespace MarketData.Domain.Interfaces;

public interface IBinanceStream : IAsyncDisposable
{
    /// <summary>Yields each Candle once it has closed, until Binance ends the stream or the token is cancelled.</summary>
    IAsyncEnumerable<Candle> ReadClosedAsync(CancellationToken cancellationToken);
}
