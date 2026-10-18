using MarketData.Domain.Symbols;

namespace MarketData.Application.Symbols;

public interface ISymbolAdministrationService
{
    /// <summary>The Monitored Symbols with the time each was added.</summary>
    Task<IReadOnlyList<MonitoredSymbol>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Starts collecting the Symbol.</summary>
    Task<SymbolAddition> AddAsync(string symbolText, CancellationToken cancellationToken);

    /// <summary>Stops collecting the Symbol and keeps its Candles; an unknown or already Removed Symbol is not an error.</summary>
    Task RemoveAsync(string symbolText, CancellationToken cancellationToken);
}
