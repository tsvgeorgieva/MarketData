using System.Buffers;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using MarketData.Domain.Candles;
using MarketData.Domain.Interfaces;
using MarketData.Domain.Symbols;
using MarketData.Infrastructure.Binance.Models;
using Microsoft.Extensions.Logging;

namespace MarketData.Infrastructure.Binance;

internal sealed class BinanceStream(ClientWebSocket socket, ILogger<BinanceStream> logger) : IBinanceStream
{
    private const int FrameSize = 16 * 1024;

    public async IAsyncEnumerable<Candle> ReadClosedAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var message in ReadMessagesAsync(cancellationToken))
        {
            if (ToClosedCandle(message) is { } candle)
            {
                yield return candle;
            }
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

    /// <summary>The Candle of a closed kline, or null while the minute is still open.</summary>
    internal static Candle? ToClosedCandle(KlineMessage message)
    {
        var kline = message.Data.Kline;
        if (!kline.IsClosed || !SymbolCode.TryParse(kline.Symbol, out var symbol))
        {
            return null;
        }

        return new Candle(
            symbol,
            DateTimeOffset.FromUnixTimeMilliseconds(kline.OpenTime),
            kline.Open,
            kline.High,
            kline.Low,
            kline.Close,
            kline.Volume);
    }

    private async IAsyncEnumerable<KlineMessage> ReadMessagesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var frame = new byte[FrameSize];
        var buffer = new ArrayBufferWriter<byte>();

        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(frame, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                yield break;
            }

            buffer.Write(frame.AsSpan(0, result.Count));
            if (!result.EndOfMessage)
            {
                continue;
            }

            if (KlineMessageParser.TryParse(buffer.WrittenMemory, out var message))
            {
                yield return message;
            }
            else
            {
                logger.LogWarning("Ignored an unrecognised Binance message of {Length} bytes", buffer.WrittenCount);
            }

            buffer.Clear();
        }
    }
}
