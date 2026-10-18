using MarketData.Domain.Symbols;

namespace MarketData.Domain.Interfaces;

public interface IMonitoredSymbolRepository
{
    /// <summary>The Symbols that are currently monitored, which excludes Removed Symbols.</summary>
    Task<IReadOnlyList<SymbolCode>> GetAllAsync(CancellationToken cancellationToken);
}
