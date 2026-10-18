using MarketData.Domain.Exceptions;
using MarketData.Domain.Interfaces;
using MarketData.Domain.Symbols;

namespace MarketData.Application.Symbols;

public sealed class SymbolAdministrationService(IMonitoredSymbolRepository monitoredSymbolRepository, TimeProvider clock) : ISymbolAdministrationService
{
    public Task<IReadOnlyList<MonitoredSymbol>> ListAsync(CancellationToken cancellationToken) =>
        monitoredSymbolRepository.GetAllAsync(cancellationToken);

    public Task RemoveAsync(string symbolText, CancellationToken cancellationToken)
    {
        var symbol = SymbolCode.TryParse(symbolText, out var parsed)
            ? parsed
            : throw new InvalidInputException($"The symbol must be {SymbolCode.MinLength} to {SymbolCode.MaxLength} letters or digits.");

        return monitoredSymbolRepository.RemoveAsync(symbol, clock.GetUtcNow(), cancellationToken);
    }
}
