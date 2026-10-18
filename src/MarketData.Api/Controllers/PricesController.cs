using MarketData.Api.Contracts;
using MarketData.Api.Mappers;
using MarketData.Application.Prices;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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
        string symbol, [BindRequired] int n, [BindRequired] string p, DateTime? e, CancellationToken cancellationToken)
    {
        var average = await priceAnalyticsService.GetSimpleMovingAverageAsync(symbol, n, p, e, cancellationToken);
        return PriceResponseMapper.ToResponse(average);
    }
}
