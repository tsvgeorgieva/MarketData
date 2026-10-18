using MarketData.Api.Contracts;
using MarketData.Domain.Symbols;

namespace MarketData.Api.Mappers;

internal static class SymbolResponseMapper
{
    public static MonitoredSymbolsResponse ToResponse(IEnumerable<MonitoredSymbol> symbols) => new()
    {
        Symbols = [.. symbols.Select(monitored => new MonitoredSymbolResponse
        {
            Symbol = monitored.Symbol.Value,
            AddedAt = monitored.AddedAt.UtcDateTime,
        })],
    };
}
