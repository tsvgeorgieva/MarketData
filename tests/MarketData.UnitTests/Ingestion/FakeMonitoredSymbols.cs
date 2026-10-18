using MarketData.Application.Symbols;
using MarketData.Domain.Symbols;

namespace MarketData.UnitTests.Ingestion;

internal sealed class FakeMonitoredSymbols(params string[] symbols) : IMonitoredSymbolRepository
{
    public Task<IReadOnlyList<SymbolCode>> ListMonitoredAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SymbolCode>>([.. symbols.Select(SymbolCode.Parse)]);
}
