using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MarketData.Domain.Exceptions;
using MarketData.Domain.Interfaces;
using MarketData.Domain.Symbols;
using MarketData.Infrastructure.Binance.Models;
using Microsoft.Extensions.Options;

namespace MarketData.Infrastructure.Binance;

/// <summary>Asks the Binance REST exchange-info endpoint whether a Symbol exists and is trading.</summary>
internal sealed class BinanceSymbolCatalog : IBinanceSymbolCatalog, IDisposable
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _http;

    public BinanceSymbolCatalog(IOptions<BinanceOptions> options) =>
        _http = new HttpClient { BaseAddress = new Uri(options.Value.RestUrl), Timeout = RequestTimeout };

    public async Task<bool> IsTradingAsync(SymbolCode symbol, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync($"/api/v3/exchangeInfo?symbol={symbol.Value}", cancellationToken);

            // Binance answers 400 (code -1121) for a symbol it does not list.
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                return false;
            }

            response.EnsureSuccessStatusCode();
            var info = await response.Content.ReadFromJsonAsync<ExchangeInfo>(cancellationToken) ?? throw new JsonException("Binance sent an empty answer.");
            return info.Symbols.Any(listed => listed.Symbol == symbol.Value && listed.Status == ExchangeSymbol.TradingStatus);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw new UpstreamUnavailableException("Binance did not answer the symbol check.");
        }
    }

    public void Dispose() => _http.Dispose();
}
