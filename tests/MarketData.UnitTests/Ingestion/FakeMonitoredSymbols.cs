using MarketData.Domain.Interfaces;
using MarketData.Domain.Symbols;

namespace MarketData.UnitTests.Ingestion;

internal sealed class FakeMonitoredSymbols(params string[] symbols) : IMonitoredSymbolRepository
{
    public Task<IReadOnlyList<MonitoredSymbol>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MonitoredSymbol>>([.. symbols.Select(symbol => new MonitoredSymbol(SymbolCode.Parse(symbol), DateTimeOffset.UnixEpoch))]);

    public Task<bool> ExistsAsync(SymbolCode symbol, CancellationToken cancellationToken) =>
        Task.FromResult(symbols.Contains(symbol.Value));

    public Task RemoveAsync(SymbolCode symbol, DateTimeOffset removedAt, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
