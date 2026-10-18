using MarketData.Domain.Exceptions;
using MarketData.Domain.Prices;

namespace MarketData.UnitTests.Domain;

public class PeriodTests
{
    [Theory]
    [InlineData("1M")]
    [InlineData("5M")]
    [InlineData("1W")]
    [InlineData("2h")]
    [InlineData("")]
    [InlineData(null)]
    public void Parse_UnknownOrDifferentlyCasedCode_ThrowsInvalidInput(string? code)
    {
        Assert.Throws<InvalidInputException>(() => Period.Parse(code));
    }
}
