using System.Net;
using System.Text.Json;
using MarketData.Domain.Exceptions;
using MarketData.Domain.Interfaces;
using MarketData.Domain.Symbols;
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
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            return document.RootElement.GetProperty("symbols").EnumerateArray()
                .Any(listed => listed.GetProperty("symbol").GetString() == symbol.Value
                    && listed.GetProperty("status").GetString() == "TRADING");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw new UpstreamUnavailableException("Binance could not be reached to check the symbol.");
        }
    }

    public void Dispose() => _http.Dispose();
}
