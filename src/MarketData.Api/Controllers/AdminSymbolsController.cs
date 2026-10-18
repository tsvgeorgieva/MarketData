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

    [HttpDelete("{symbol}")]
    public async Task<IActionResult> Remove(string symbol, CancellationToken cancellationToken)
    {
        await symbolAdministrationService.RemoveAsync(symbol, cancellationToken);
        return NoContent();
    }
}
