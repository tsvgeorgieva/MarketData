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
using Microsoft.Extensions.Options;

namespace MarketData.Infrastructure.Binance;

/// <summary>
/// The one Binance WebSocket connection of the service. It remembers which Symbols should be subscribed, so a Symbol added or removed
/// before the connection opens is not lost, and it sends subscribe and unsubscribe requests once the connection is open.
/// </summary>
internal sealed class BinanceStream(IOptions<BinanceOptions> options, ILogger<BinanceStream> logger) : IBinanceStream
{
    private const int FrameSize = 16 * 1024;

    // A WebSocket allows one send at a time, and the subscribed set changes together with what is sent.
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HashSet<SymbolCode> _subscribed = [];
    private ClientWebSocket? _socket;
    private int _requestId;

    public async Task ConnectAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            _subscribed.UnionWith(symbols);
            var uri = new Uri($"{options.Value.WebSocketUrl}/stream?streams={string.Join('/', _subscribed.Select(StreamName))}");

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

            _socket?.Dispose();
            _socket = socket;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async IAsyncEnumerable<Candle> ReadClosedAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var socket = _socket ?? throw new InvalidOperationException("The Binance stream is not connected.");
        await foreach (var message in ReadMessagesAsync(socket, cancellationToken))
        {
            if (ToClosedCandle(message) is { } candle)
            {
                yield return candle;
            }
        }
    }

    public async Task SubscribeAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var added = symbols.Where(_subscribed.Add).ToList();
            await SendIfOpenAsync("SUBSCRIBE", added, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UnsubscribeAsync(IReadOnlyCollection<SymbolCode> symbols, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var removed = symbols.Where(_subscribed.Remove).ToList();
            await SendIfOpenAsync("UNSUBSCRIBE", removed, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_socket is { State: WebSocketState.Open } socket)
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

        _socket?.Dispose();
        _gate.Dispose();
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

    /// <summary>Sends the request when the connection is open; otherwise the change is only remembered and applies when the connection opens.</summary>
    private async Task SendIfOpenAsync(string method, List<SymbolCode> symbols, CancellationToken cancellationToken)
    {
        if (symbols.Count == 0 || _socket is not { State: WebSocketState.Open } socket)
        {
            return;
        }

        var request = Encoding.UTF8.GetBytes(BuildRequest(method, symbols, ++_requestId));
        await socket.SendAsync(request, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
    }

    private async IAsyncEnumerable<KlineMessage> ReadMessagesAsync(ClientWebSocket socket, [EnumeratorCancellation] CancellationToken cancellationToken)
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
