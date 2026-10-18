using System.Net.WebSockets;
using MarketData.Domain.Interfaces;
using MarketData.Domain.Symbols;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MarketData.Infrastructure.Binance;

internal sealed class BinanceStreamFactory(IOptions<BinanceOptions> options, ILoggerFactory loggers) : IBinanceStreamFactory
{
    public async Task<IBinanceStream> ConnectAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken)
    {
        // Stream names use lowercase symbols: one combined connection carries every kline_1m stream.
        var streams = string.Join('/', symbols.Select(BinanceStream.StreamName));
        var uri = new Uri($"{options.Value.WebSocketUrl}/stream?streams={streams}");

        var socket = new ClientWebSocket();
        try
        {
            await socket.ConnectAsync(uri, cancellationToken);
        }
        catch
        {
            socket.Dispose();
            throw;
        }

        return new BinanceStream(socket, loggers.CreateLogger<BinanceStream>());
    }
}
