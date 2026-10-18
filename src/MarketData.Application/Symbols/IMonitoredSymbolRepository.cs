using MarketData.Domain.Symbols;

namespace MarketData.Application.Symbols;

public interface IMonitoredSymbolRepository
{
    Task<IReadOnlyList<SymbolCode>> ListMonitoredAsync(CancellationToken cancellationToken);
}
