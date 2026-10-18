using System.Buffers;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using MarketData.Domain.Candles;
using MarketData.Domain.Interfaces;
using MarketData.Domain.Symbols;
using MarketData.Infrastructure.Binance.Models;
using Microsoft.Extensions.Logging;

namespace MarketData.Infrastructure.Binance;

internal sealed class BinanceStream(ClientWebSocket socket, ILogger<BinanceStream> logger) : IBinanceStream
{
    private const int FrameSize = 16 * 1024;

    // A WebSocket allows one send at a time; Subscribe and Unsubscribe calls wait their turn.
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private int _requestId;

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

    public Task SubscribeAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken) =>
        SendAsync("SUBSCRIBE", symbols, cancellationToken);

    public Task UnsubscribeAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken) =>
        SendAsync("UNSUBSCRIBE", symbols, cancellationToken);

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
        _sendLock.Dispose();
    }

    /// <summary>The request that adds or removes the one-minute kline streams of the Symbols on an open connection.</summary>
    internal static string BuildRequest(string method, IReadOnlyCollection<SymbolCode> symbols, int id) =>
        JsonSerializer.Serialize(new { method, @params = symbols.Select(StreamName), id });

    /// <summary>Whether the message is Binance's answer to a subscribe or unsubscribe request, which looks like <c>{"result":null,"id":1}</c>.</summary>
    internal static bool IsReplyToRequest(ReadOnlyMemory<byte> utf8Json)
    {
        try
        {
            using var document = JsonDocument.Parse(utf8Json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("result", out _)
                && document.RootElement.TryGetProperty("id", out _);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Stream names use lowercase symbols: one connection carries every kline_1m stream.</summary>
    internal static string StreamName(SymbolCode symbol) => $"{symbol.Value.ToLowerInvariant()}@kline_1m";

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

    private async Task SendAsync(string method, IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken)
    {
        var request = Encoding.UTF8.GetBytes(BuildRequest(method, symbols, Interlocked.Increment(ref _requestId)));
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await socket.SendAsync(request, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
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
            else if (IsReplyToRequest(buffer.WrittenMemory))
            {
                logger.LogDebug("Binance answered a subscribe or unsubscribe request");
            }
            else
            {
                logger.LogWarning("Ignored an unrecognised Binance message of {Length} bytes", buffer.WrittenCount);
            }

            buffer.Clear();
        }
    }
}
