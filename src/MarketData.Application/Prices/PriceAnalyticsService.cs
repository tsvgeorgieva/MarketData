using MarketData.Domain.Exceptions;
using MarketData.Domain.Interfaces;
using MarketData.Domain.Prices;
using MarketData.Domain.Symbols;

namespace MarketData.Application.Prices;

public sealed class PriceAnalyticsService(
    IMonitoredSymbolRepository monitoredSymbolRepository,
    ICandlePriceAnalyticsRepository candlePriceAnalyticsRepository,
    TimeProvider clock) : IPriceAnalyticsService
{
    private static readonly TimeSpan Day = TimeSpan.FromHours(24);

    public async Task<AveragePrice> GetAveragePrice24hAsync(string symbolText, CancellationToken cancellationToken)
    {
        if (!SymbolCode.TryParse(symbolText, out var symbol))
        {
            throw new InvalidInputException($"The symbol must be {SymbolCode.MinLength} to {SymbolCode.MaxLength} letters or digits.");
        }

        if (!await monitoredSymbolRepository.ExistsAsync(symbol, cancellationToken))
        {
            throw new NotFoundException($"{symbol} is not a monitored symbol.");
        }

        var now = clock.GetUtcNow();
        return await candlePriceAnalyticsRepository.GetAveragePriceAsync(symbol, now - Day, now, cancellationToken)
            ?? throw new NotFoundException($"No data collected yet for {symbol} in the last 24 hours.");
    }
}
