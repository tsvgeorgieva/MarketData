using System.Globalization;
using System.Text.Json;
using MarketData.Domain.Candles;
using MarketData.Domain.Exceptions;
using MarketData.Domain.Interfaces;
using MarketData.Domain.Symbols;
using Microsoft.Extensions.Options;

namespace MarketData.Infrastructure.Binance;

/// <summary>Reads closed one-minute Candles from the Binance REST klines endpoint, one page of up to 1,000 at a time.</summary>
internal sealed class BinanceHistory : IBinanceHistory, IDisposable
{
    private const int PageSize = 1000;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _http;

    public BinanceHistory(IOptions<BinanceOptions> options) =>
        _http = new HttpClient { BaseAddress = new Uri(options.Value.RestUrl), Timeout = RequestTimeout };

    public async Task<IReadOnlyList<Candle>> GetClosedCandlesAsync(SymbolCode symbol, DateTimeOffset from, DateTimeOffset through, CancellationToken cancellationToken)
    {
        var candles = new List<Candle>();
        var next = from;
        while (next <= through)
        {
            var page = await GetPageAsync(symbol, next, through, cancellationToken);
            candles.AddRange(page);
            if (page.Count < PageSize)
            {
                break;
            }

            next = page[^1].OpenTime + Candle.Length;
        }

        return candles;
    }

    public void Dispose() => _http.Dispose();

    private async Task<List<Candle>> GetPageAsync(SymbolCode symbol, DateTimeOffset from, DateTimeOffset through, CancellationToken cancellationToken)
    {
        // startTime and endTime are inclusive and compare with a kline's open time, so ending at the last closed Candle's open time excludes the forming one.
        var query = string.Create(
            CultureInfo.InvariantCulture,
            $"/api/v3/klines?symbol={symbol.Value}&interval=1m&startTime={from.ToUnixTimeMilliseconds()}&endTime={through.ToUnixTimeMilliseconds()}&limit={PageSize}");
        try
        {
            using var response = await _http.GetAsync(query, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            return KlineHistoryParser.Parse(body.RootElement, symbol);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or FormatException or InvalidOperationException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw new UpstreamUnavailableException("Binance did not answer the history request.");
        }
    }
}
