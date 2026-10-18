namespace MarketData.Application.Ingestion;

public interface IBinanceStream : IAsyncDisposable
{
    /// <summary>Yields updates until Binance closes the stream or the token is cancelled.</summary>
    IAsyncEnumerable<KlineUpdate> ReadAsync(CancellationToken cancellationToken);
}
