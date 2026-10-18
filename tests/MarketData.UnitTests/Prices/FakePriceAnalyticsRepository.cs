using MarketData.Domain.Interfaces;
using MarketData.Domain.Prices;
using MarketData.Domain.Symbols;

namespace MarketData.UnitTests.Prices;

/// <summary>Answers with a scripted average and remembers the window it was asked for.</summary>
internal sealed class FakePriceAnalyticsRepository : ICandlePriceAnalyticsRepository
{
    /// <summary>What <see cref="GetAveragePriceAsync"/> answers; null means no Candles.</summary>
    public AveragePrice? Result { get; set; }

    public (DateTimeOffset Start, DateTimeOffset End)? RequestedWindow { get; private set; }

    public Task<AveragePrice?> GetAveragePriceAsync(SymbolCode symbol, DateTimeOffset windowStart, DateTimeOffset windowEnd, CancellationToken cancellationToken)
    {
        RequestedWindow = (windowStart, windowEnd);
        return Task.FromResult(Result);
    }
}
