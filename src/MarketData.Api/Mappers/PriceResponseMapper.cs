using MarketData.Api.Contracts;
using MarketData.Domain.Prices;

namespace MarketData.Api.Mappers;

internal static class PriceResponseMapper
{
    public static AveragePriceResponse ToResponse(AveragePrice average) => new()
    {
        Symbol = average.Symbol.Value,
        From = average.From.UtcDateTime,
        To = average.To.UtcDateTime,
        Average = average.Average,
        DataPoints = average.DataPoints,
    };

    public static SimpleMovingAverageResponse ToResponse(SimpleMovingAverage average) => new()
    {
        Symbol = average.Symbol.Value,
        Period = average.Period.Code,
        Requested = average.Requested,
        From = average.From,
        To = average.To,
        Average = average.Average,
        DataPoints = average.DataPoints,
        InProgress = average.InProgress,
    };

    /// <summary>Validated here at the edge of the domain: the model binder has already made sure <c>n</c> and <c>p</c> are present.</summary>
    public static Domain.Prices.SimpleMovingAverageRequest ToRequest(string symbol, Contracts.SimpleMovingAverageRequest query) =>
        new(symbol, query.N!.Value, query.P!, query.E);
}
