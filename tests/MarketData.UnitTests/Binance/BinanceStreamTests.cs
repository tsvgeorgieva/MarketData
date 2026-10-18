using MarketData.Domain.Candles;
using MarketData.Domain.Symbols;
using MarketData.Infrastructure.Binance;

namespace MarketData.UnitTests.Binance;

public class BinanceStreamTests
{
    [Fact]
    public void ToClosedCandle_ClosedKline_MapsToTheDomainCandle()
    {
        Assert.True(KlineMessageParser.TryParse(SampleKlineMessage.Json(closed: true), out var message));

        var candle = BinanceStream.ToClosedCandle(message);

        Assert.Equal(
            new Candle(SymbolCode.Parse("BTCUSDT"), new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero), 100.5m, 102.25m, 99.75m, 101.125m, 2.5m),
            candle);
    }

    [Fact]
    public void ToClosedCandle_KlineStillOpen_ReturnsNothing()
    {
        Assert.True(KlineMessageParser.TryParse(SampleKlineMessage.Json(closed: false), out var message));

        Assert.Null(BinanceStream.ToClosedCandle(message));
    }

    [Fact]
    public void BuildRequest_Subscribe_NamesTheLowercaseOneMinuteStreams()
    {
        var json = BinanceStream.BuildRequest("SUBSCRIBE", [SymbolCode.Parse("XRPUSDT"), SymbolCode.Parse("ADAUSDT")], id: 7);

        Assert.Equal("""{"method":"SUBSCRIBE","params":["xrpusdt@kline_1m","adausdt@kline_1m"],"id":7}""", json);
    }

    [Theory]
    [InlineData("""{"result":null,"id":7}""", true)]
    [InlineData("""{"code":2,"msg":"Invalid request"}""", false)]
    [InlineData("not json", false)]
    public void IsReplyToRequest_Message_IsRecognisedOnlyForABinanceReply(string message, bool expected) =>
        Assert.Equal(expected, BinanceStream.IsReplyToRequest(System.Text.Encoding.UTF8.GetBytes(message)));
}
