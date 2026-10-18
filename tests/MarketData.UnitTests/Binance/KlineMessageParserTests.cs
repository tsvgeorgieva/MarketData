using System.Text;
using MarketData.Infrastructure.Binance;

namespace MarketData.UnitTests.Binance;

public class KlineMessageParserTests
{
    [Fact]
    public void TryParse_ClosedKline_ReturnsTheCandle()
    {
        var parsed = KlineMessageParser.TryParse(Message(closed: true), out var update);

        Assert.True(parsed);
        var candle = update!.Candle;
        Assert.Equal(
            ("BTCUSDT", new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero), 100.5m, 102.25m, 99.75m, 101.125m, 2.5m),
            (candle.Symbol.Value, candle.OpenTime, candle.Open, candle.High, candle.Low, candle.Close, candle.Volume));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TryParse_Kline_ReportsWhetherItIsClosed(bool closed)
    {
        Assert.True(KlineMessageParser.TryParse(Message(closed), out var update));

        Assert.Equal(closed, update!.IsClosed);
    }

    [Fact]
    public void TryParse_SubscriptionReply_IsNotAKline()
    {
        Assert.False(KlineMessageParser.TryParse(Utf8("""{"result":null,"id":1}"""), out _));
    }

    [Fact]
    public void TryParse_OtherEventType_IsNotAKline()
    {
        Assert.False(KlineMessageParser.TryParse(Utf8("""{"stream":"x","data":{"e":"24hrTicker","s":"BTCUSDT"}}"""), out _));
    }

    [Fact]
    public void TryParse_OtherInterval_IsIgnored()
    {
        var message = Message(closed: true).ToArray();
        var text = Encoding.UTF8.GetString(message).Replace("\"i\":\"1m\"", "\"i\":\"5m\"", StringComparison.Ordinal);

        Assert.False(KlineMessageParser.TryParse(Utf8(text), out _));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"stream":"x","data":{"e":"kline","k":{}}}""")]
    [InlineData("""{"stream":"x","data":{"e":"kline","k":{"i":"1m","s":"BTCUSDT","t":1,"o":"abc","h":"1","l":"1","c":"1","v":"1","x":true}}}""")]
    public void TryParse_MalformedMessage_IsRejectedWithoutThrowing(string text)
    {
        Assert.False(KlineMessageParser.TryParse(Utf8(text), out _));
    }

    // Binance uses keys that differ only by case (v and V, t and T): the volume must come from "v".
    private static ReadOnlyMemory<byte> Message(bool closed) => Utf8(
        """
        {"stream":"btcusdt@kline_1m","data":{"e":"kline","E":1767268860000,"s":"BTCUSDT","k":{
        "t":1767268800000,"T":1767268859999,"s":"BTCUSDT","i":"1m","f":1,"L":2,
        "o":"100.50000000","c":"101.12500000","h":"102.25000000","l":"99.75000000",
        "v":"2.50000000","n":398,"x":CLOSED,"q":"63213.0","V":"0.21","Q":"17149.2","B":"0"}}}
        """.Replace("CLOSED", closed ? "true" : "false", StringComparison.Ordinal));

    private static ReadOnlyMemory<byte> Utf8(string text) => Encoding.UTF8.GetBytes(text);
}
