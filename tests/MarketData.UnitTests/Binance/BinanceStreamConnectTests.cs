using System.Net;
using System.Net.Sockets;
using MarketData.Infrastructure.Binance;
using MarketData.Domain.Symbols;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarketData.UnitTests.Binance;

/// <summary>Uses a local listener that never answers the WebSocket handshake, only to see whether the stream tries to connect.</summary>
public sealed class BinanceStreamConnectTests : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly BinanceStream _stream;

    public BinanceStreamConnectTests()
    {
        _listener.Start();
        var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _stream = new BinanceStream(
            Options.Create(new BinanceOptions { WebSocketUrl = $"ws://127.0.0.1:{port}", RestUrl = $"http://127.0.0.1:{port}" }),
            NullLogger<BinanceStream>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        await _stream.DisposeAsync();
        _stop.Dispose();
    }

    [Fact]
    public async Task ConnectAsync_NoSymbols_WaitsAndDoesNotTryToConnect()
    {
        var connecting = _stream.ConnectAsync([], _stop.Token);

        await Task.Delay(500, TestContext.Current.CancellationToken);

        Assert.False(_listener.Pending() || connecting.IsCompleted);
    }

    [Fact]
    public async Task ConnectAsync_NoSymbols_WaitingEndsWithTheCancellation()
    {
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _stream.ConnectAsync([], cancel.Token));
    }

    [Fact]
    public async Task ConnectAsync_NoSymbols_ConnectsOnceASymbolIsSubscribed()
    {
        _ = _stream.ConnectAsync([], _stop.Token);

        await _stream.SubscribeAsync([SymbolCode.Parse("XRPUSDT")], TestContext.Current.CancellationToken);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (!_listener.Pending())
        {
            await Task.Delay(20, timeout.Token);
        }
    }

    [Fact]
    public async Task ConnectAsync_WithSymbols_ConnectsRightAway()
    {
        _ = _stream.ConnectAsync([SymbolCode.Parse("BTCUSDT")], _stop.Token);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (!_listener.Pending())
        {
            await Task.Delay(20, timeout.Token);
        }
    }
}
