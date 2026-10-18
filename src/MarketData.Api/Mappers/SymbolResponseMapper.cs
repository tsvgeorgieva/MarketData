using MarketData.Api.Contracts;
using MarketData.Domain.Symbols;

namespace MarketData.Api.Mappers;

internal static class SymbolResponseMapper
{
    public static MonitoredSymbolsResponse ToResponse(IEnumerable<MonitoredSymbol> symbols) => new()
    {
        Symbols = [.. symbols.Select(ToResponse)],
    };

    public static MonitoredSymbolResponse ToResponse(MonitoredSymbol monitored) => new()
    {
        Symbol = monitored.Symbol.Value,
        AddedAt = monitored.AddedAt.UtcDateTime,
    };
}
