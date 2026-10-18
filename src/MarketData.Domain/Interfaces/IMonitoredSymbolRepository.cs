using MarketData.Domain.Symbols;

namespace MarketData.Domain.Interfaces;

public interface IMonitoredSymbolRepository
{
    /// <summary>The Symbols that are currently monitored with the time each was added, by Symbol; excludes Removed Symbols.</summary>
    Task<IReadOnlyList<MonitoredSymbol>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>Whether the Symbol is currently monitored; false for an unknown or Removed Symbol.</summary>
    Task<bool> ExistsAsync(SymbolCode symbol, CancellationToken cancellationToken);

    /// <summary>Starts monitoring the Symbol as of the given time.</summary>
    Task<SymbolAddition> AddAsync(SymbolCode symbol, DateTimeOffset addedAt, CancellationToken cancellationToken);

    /// <summary>Marks the Symbol as removed at the given time and keeps its row; does nothing for an unknown or Removed Symbol.</summary>
    Task RemoveAsync(SymbolCode symbol, DateTimeOffset removedAt, CancellationToken cancellationToken);
}
