using System.Text;
using System.Text.Json;
using MarketData.Domain.Candles;
using MarketData.Domain.Symbols;
using MarketData.Infrastructure.Binance;

namespace MarketData.UnitTests.Binance;

public class KlineHistoryParserTests
{
    private static readonly SymbolCode Btc = SymbolCode.Parse("BTCUSDT");

    [Fact]
    public void Parse_KlinesAnswer_MapsEachToACandleOldestFirst()
    {
        const string json = """
            [
              [1767268800000,"100.5","102.25","99.75","101.125","2.5",1767268859999,"253.1",10,"1.2","121.0","0"],
              [1767268860000,"101.125","103","101","102","3",1767268919999,"306.0",12,"1.5","153.0","0"]
            ]
            """;

        var candles = KlineHistoryParser.Parse(Encoding.UTF8.GetBytes(json), Btc);

        Assert.Equal(
            [
                new Candle(Btc, new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero), 100.5m, 102.25m, 99.75m, 101.125m, 2.5m),
                new Candle(Btc, new DateTimeOffset(2026, 1, 1, 12, 1, 0, TimeSpan.Zero), 101.125m, 103m, 101m, 102m, 3m),
            ],
            candles);
    }

    [Fact]
    public void Parse_NoKlines_ReturnsNothing() =>
        Assert.Empty(KlineHistoryParser.Parse("[]"u8.ToArray(), Btc));

    [Fact]
    public void Parse_KlineWithTooFewFields_Throws() =>
        Assert.Throws<JsonException>(() => KlineHistoryParser.Parse("""[[1767268800000,"100.5"]]"""u8.ToArray(), Btc));

    [Fact]
    public void Parse_AnswerThatIsNotAnArray_Throws() =>
        Assert.Throws<JsonException>(() => KlineHistoryParser.Parse("""{"code":-1121}"""u8.ToArray(), Btc));
}
