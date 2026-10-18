using MarketData.Domain.Candles;

namespace MarketData.Application.Ingestion;

public interface ICandleStore
{
    /// <summary>Stores the Candles that are not stored yet and leaves existing ones untouched. Returns how many were added.</summary>
    Task<int> InsertMissingAsync(IReadOnlyCollection<Candle> candles, CancellationToken cancellationToken);
}
