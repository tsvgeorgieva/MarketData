using MarketData.Application.Ingestion;
using MarketData.Domain.Exceptions;
using MarketData.Domain.Interfaces;
using MarketData.Domain.Symbols;
using Microsoft.Extensions.Options;

namespace MarketData.Application.Symbols;

public sealed class SymbolAdministrationService(
    IMonitoredSymbolRepository monitoredSymbolRepository,
    IBinanceSymbolCatalog binanceSymbolCatalog,
    BackfillService backfillService,
    IOptions<SymbolsOptions> options,
    TimeProvider clock) : ISymbolAdministrationService
{
    public Task<IReadOnlyList<MonitoredSymbol>> ListAsync(CancellationToken cancellationToken) =>
        monitoredSymbolRepository.GetAllAsync(cancellationToken);

    public async Task<(MonitoredSymbol Symbol, bool Added)> AddAsync(string symbolText, CancellationToken cancellationToken)
    {
        var symbol = Parse(symbolText);
        var existing = await monitoredSymbolRepository.FindAsync(symbol, cancellationToken);
        if (existing is not null)
        {
            return (existing, false);
        }

        if (await monitoredSymbolRepository.CountAsync(cancellationToken) >= options.Value.MaxMonitored)
        {
            throw new ConflictException($"At most {options.Value.MaxMonitored} symbols can be monitored.");
        }

        if (!await binanceSymbolCatalog.IsTradingAsync(symbol, cancellationToken))
        {
            throw new UnprocessableException($"{symbol} is not a symbol that Binance is trading.");
        }

        var added = await monitoredSymbolRepository.AddAsync(symbol, clock.GetUtcNow(), cancellationToken);
        // The symbol is saved, so a client that disconnects now must not cancel its backfill.
        await backfillService.BackfillAsync(symbol, CancellationToken.None);
        return (added, true);
    }

    public Task RemoveAsync(string symbolText, CancellationToken cancellationToken) =>
        monitoredSymbolRepository.RemoveAsync(Parse(symbolText), clock.GetUtcNow(), cancellationToken);

    private static SymbolCode Parse(string symbolText) =>
        SymbolCode.TryParse(symbolText, out var parsed)
            ? parsed
            : throw new InvalidInputException($"The symbol must be {SymbolCode.MinLength} to {SymbolCode.MaxLength} letters or digits.");
}
