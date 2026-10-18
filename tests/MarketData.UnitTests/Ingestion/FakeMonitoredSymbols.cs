using MarketData.Domain.Interfaces;
using MarketData.Domain.Symbols;

namespace MarketData.UnitTests.Ingestion;

internal sealed class FakeMonitoredSymbols(params string[] initial) : IMonitoredSymbolRepository
{
    private readonly List<string> _symbols = [.. initial];
    private readonly Lock _lock = new();

    public void Add(string symbol)
    {
        lock (_lock)
        {
            _symbols.Add(symbol);
        }
    }

    public void Remove(string symbol)
    {
        lock (_lock)
        {
            _symbols.Remove(symbol);
        }
    }

    public Task<IReadOnlyList<MonitoredSymbol>> GetAllAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<MonitoredSymbol>>([.. _symbols.Select(symbol => new MonitoredSymbol(SymbolCode.Parse(symbol), DateTimeOffset.UnixEpoch))]);
        }
    }

    public Task<bool> ExistsAsync(SymbolCode symbol, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            return Task.FromResult(_symbols.Contains(symbol.Value));
        }
    }

    public Task<MonitoredSymbol?> FindAsync(SymbolCode symbol, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<int> CountAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            return Task.FromResult(_symbols.Count);
        }
    }

    public Task<MonitoredSymbol> AddAsync(SymbolCode symbol, DateTimeOffset addedAt, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task RemoveAsync(SymbolCode symbol, DateTimeOffset removedAt, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
