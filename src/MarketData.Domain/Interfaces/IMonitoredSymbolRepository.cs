using MarketData.Domain.Symbols;

namespace MarketData.Domain.Interfaces;

public interface IMonitoredSymbolRepository
{
    /// <summary>The Symbols that are currently monitored, which excludes Removed Symbols.</summary>
    Task<IReadOnlyList<SymbolCode>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>Whether the Symbol is currently monitored; false for an unknown or Removed Symbol.</summary>
    Task<bool> ExistsAsync(SymbolCode symbol, CancellationToken cancellationToken);

    /// <summary>The currently monitored Symbols with the time each was added, by Symbol.</summary>
    Task<IReadOnlyList<MonitoredSymbol>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Marks the Symbol as removed at the given time and keeps its row; does nothing for an unknown or Removed Symbol.</summary>
    Task RemoveAsync(SymbolCode symbol, DateTimeOffset removedAt, CancellationToken cancellationToken);
}
