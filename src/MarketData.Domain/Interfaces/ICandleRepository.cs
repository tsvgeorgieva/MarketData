using MarketData.Domain.Candles;

namespace MarketData.Domain.Interfaces;

public interface ICandleRepository
{
    /// <summary>Stores the Candles that are not stored yet and leaves existing ones untouched. Returns how many were added.</summary>
    Task<int> InsertMissingAsync(IReadOnlyCollection<Candle> candles, CancellationToken cancellationToken);
}
