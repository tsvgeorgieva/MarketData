using MarketData.Api.Contracts;
using MarketData.Api.Mappers;
using MarketData.Application.Prices.Interfaces;
using MarketData.Application.Prices;
using Microsoft.AspNetCore.Mvc;

namespace MarketData.Api.Controllers;

[ApiController]
[Route("api/{symbol}")]
public class PricesController(IPriceAnalyticsService priceAnalyticsService) : ControllerBase
{
    [HttpGet("24hAvgPrice")]
    public async Task<ActionResult<AveragePriceResponse>> GetAveragePrice24h(string symbol, CancellationToken cancellationToken)
    {
        var average = await priceAnalyticsService.GetAveragePrice24hAsync(symbol, cancellationToken);
        return PriceResponseMapper.ToResponse(average);
    }

    [HttpGet("SimpleMovingAverage")]
    public async Task<ActionResult<SimpleMovingAverageResponse>> GetSimpleMovingAverage(
        string symbol, [FromQuery] Contracts.SimpleMovingAverageRequest query, CancellationToken cancellationToken)
    {
        var average = await priceAnalyticsService.GetSimpleMovingAverageAsync(PriceResponseMapper.ToRequest(symbol, query), cancellationToken);
        return PriceResponseMapper.ToResponse(average);
    }
}
