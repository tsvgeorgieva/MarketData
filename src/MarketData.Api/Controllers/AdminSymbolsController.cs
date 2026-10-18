using MarketData.Api.Admin;
using MarketData.Api.Contracts;
using MarketData.Api.Mappers;
using MarketData.Application.Symbols;
using Microsoft.AspNetCore.Mvc;

namespace MarketData.Api.Controllers;

[ApiController]
[AdminApiKey]
[Route("api/admin/symbols")]
public class AdminSymbolsController(ISymbolAdministrationService symbolAdministrationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<MonitoredSymbolsResponse>> List(CancellationToken cancellationToken) =>
        SymbolResponseMapper.ToResponse(await symbolAdministrationService.ListAsync(cancellationToken));

    [HttpPut("{symbol}")]
    public async Task<ActionResult<MonitoredSymbolResponse>> Add(string symbol, CancellationToken cancellationToken)
    {
        var addition = await symbolAdministrationService.AddAsync(symbol, cancellationToken);
        var response = SymbolResponseMapper.ToResponse(addition.Monitored);
        return addition.Added ? StatusCode(StatusCodes.Status201Created, response) : response;
    }

    [HttpDelete("{symbol}")]
    public async Task<IActionResult> Remove(string symbol, CancellationToken cancellationToken)
    {
        await symbolAdministrationService.RemoveAsync(symbol, cancellationToken);
        return NoContent();
    }
}
