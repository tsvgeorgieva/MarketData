using MarketData.Domain.Interfaces;
using MarketData.Domain.Candles;
using MarketData.Domain.Prices;
using MarketData.Domain.Symbols;

namespace MarketData.IntegrationTests.Infrastructure;

/// <summary>A repository whose reads fail with a message that must never reach an API client.</summary>
public sealed class ThrowingPriceAnalyticsRepository : ICandlePriceAnalyticsRepository
{
    public const string SecretDetail = "secret internal detail";

    public Task<AveragePrice?> GetAveragePriceAsync(SymbolCode symbol, DateTimeOffset windowStart, DateTimeOffset windowEnd, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(SecretDetail);

    public Task<IReadOnlyList<Candle>> GetLastCandlesPerPeriodAsync(SymbolCode symbol, Period period, DateTime from, DateTime through, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(SecretDetail);
}
