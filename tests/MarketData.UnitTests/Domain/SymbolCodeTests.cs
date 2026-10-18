using MarketData.Domain.Symbols;

namespace MarketData.UnitTests.Domain;

public class SymbolCodeTests
{
    [Theory]
    [InlineData("BTCUSDT", "BTCUSDT")]
    [InlineData("btcusdt", "BTCUSDT")]
    [InlineData("  ethusdt\t", "ETHUSDT")]
    [InlineData("1INCHUSDT", "1INCHUSDT")]
    public void TryParse_ValidText_NormalisesToTrimmedUppercase(string text, string expected)
    {
        var parsed = SymbolCode.TryParse(text, out var code);

        Assert.True(parsed);
        Assert.Equal(expected, code!.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("AB")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTU")]
    [InlineData("BTC-USDT")]
    [InlineData("BTC USDT")]
    [InlineData("ÅÄÖUSDT")]
    public void TryParse_InvalidText_IsRejected(string? text)
    {
        Assert.False(SymbolCode.TryParse(text, out _));
    }

    [Fact]
    public void Parse_InvalidText_ThrowsFormatException()
    {
        Assert.Throws<FormatException>(() => SymbolCode.Parse("no"));
    }

    [Fact]
    public void Equality_SameValueDifferentCase_IsEqual()
    {
        Assert.Equal(SymbolCode.Parse("btcusdt"), SymbolCode.Parse("BTCUSDT"));
    }
}
