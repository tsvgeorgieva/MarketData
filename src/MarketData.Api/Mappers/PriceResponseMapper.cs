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
}
