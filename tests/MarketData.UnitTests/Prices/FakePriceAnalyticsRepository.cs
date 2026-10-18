using MarketData.Domain.Interfaces;
using MarketData.Domain.Prices;
using MarketData.Domain.Symbols;

namespace MarketData.UnitTests.Prices;

/// <summary>Answers with a scripted average and remembers what it was asked for.</summary>
internal sealed class FakePriceAnalyticsRepository : ICandlePriceAnalyticsRepository
{
    /// <summary>What the repository answers; null means no Candles.</summary>
    public AveragePrice? Result { get; set; }

    public (Period Period, DateTime From, DateTime Through)? Requested { get; private set; }

    public Task<AveragePrice?> GetAveragePriceOfLastCandlesPerPeriodAsync(SymbolCode symbol, Period period, DateTime from, DateTime through, CancellationToken cancellationToken)
    {
        Requested = (period, from, through);
        return Task.FromResult(Result);
    }
}
