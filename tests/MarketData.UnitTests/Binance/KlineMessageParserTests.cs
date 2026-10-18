using System.Text;
using MarketData.Infrastructure.Binance;

namespace MarketData.UnitTests.Binance;

public class KlineMessageParserTests
{
    [Fact]
    public void TryParse_ClosedKline_ReturnsTheKlineFields()
    {
        var parsed = KlineMessageParser.TryParse(SampleKlineMessage.Json(closed: true), out var message);

        Assert.True(parsed);
        var kline = message!.Data.Kline;
        Assert.Equal(
            ("BTCUSDT", 1767268800000L, 100.5m, 102.25m, 99.75m, 101.125m, 2.5m, true),
            (kline.Symbol, kline.OpenTime, kline.Open, kline.High, kline.Low, kline.Close, kline.Volume, kline.IsClosed));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TryParse_Kline_ReportsWhetherItIsClosed(bool closed)
    {
        Assert.True(KlineMessageParser.TryParse(SampleKlineMessage.Json(closed), out var message));

        Assert.Equal(closed, message.Data.Kline.IsClosed);
    }

    [Fact]
    public void TryParse_SubscriptionReply_IsNotAKline()
    {
        Assert.False(KlineMessageParser.TryParse(SampleKlineMessage.Utf8("""{"result":null,"id":1}"""), out _));
    }

    [Fact]
    public void TryParse_OtherEventType_IsNotAKline()
    {
        Assert.False(KlineMessageParser.TryParse(
            SampleKlineMessage.Utf8("""{"stream":"x","data":{"e":"24hrTicker","k":{"s":"BTCUSDT","i":"1m","t":1,"o":"1","h":"1","l":"1","c":"1","v":"1","x":true}}}"""),
            out _));
    }

    [Fact]
    public void TryParse_OtherInterval_IsIgnored()
    {
        var text = Encoding.UTF8.GetString(SampleKlineMessage.Json(closed: true).Span)
            .Replace("\"i\":\"1m\"", "\"i\":\"5m\"", StringComparison.Ordinal);

        Assert.False(KlineMessageParser.TryParse(SampleKlineMessage.Utf8(text), out _));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"stream":"x","data":{"e":"kline","k":{}}}""")]
    [InlineData("""{"stream":"x","data":{"e":"kline","k":{"i":"1m","s":"BTCUSDT","t":1,"o":"abc","h":"1","l":"1","c":"1","v":"1","x":true}}}""")]
    public void TryParse_MalformedMessage_IsRejectedWithoutThrowing(string text)
    {
        Assert.False(KlineMessageParser.TryParse(SampleKlineMessage.Utf8(text), out _));
    }
}
