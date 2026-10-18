using System.Buffers;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using MarketData.Application.Ingestion;
using Microsoft.Extensions.Logging;

namespace MarketData.Infrastructure.Binance;

internal sealed class BinanceStream(ClientWebSocket socket, ILogger<BinanceStream> logger) : IBinanceStream
{
    private const int FrameSize = 16 * 1024;

    public async IAsyncEnumerable<KlineUpdate> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var frame = new byte[FrameSize];
        var message = new ArrayBufferWriter<byte>();

        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(frame, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                yield break;
            }

            message.Write(frame.AsSpan(0, result.Count));
            if (!result.EndOfMessage)
            {
                continue;
            }

            if (KlineMessageParser.TryParse(message.WrittenMemory, out var update))
            {
                yield return update;
            }
            else
            {
                logger.LogWarning("Ignored an unrecognised Binance message of {Length} bytes", message.WrittenCount);
            }

            message.Clear();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (socket.State == WebSocketState.Open)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try
            {
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, timeout.Token);
            }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
            {
                logger.LogDebug(ex, "The Binance socket was not closed cleanly");
            }
        }

        socket.Dispose();
    }
}
