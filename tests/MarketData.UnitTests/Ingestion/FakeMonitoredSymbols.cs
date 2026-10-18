using MarketData.Domain.Interfaces;
using MarketData.Domain.Symbols;

namespace MarketData.UnitTests.Ingestion;

internal sealed class FakeMonitoredSymbols(params string[] symbols) : IMonitoredSymbolRepository
{
    public Task<IReadOnlyList<SymbolCode>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SymbolCode>>([.. symbols.Select(SymbolCode.Parse)]);
}
