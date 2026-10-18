using MarketData.Application.Prices;
using MarketData.Domain.Exceptions;
using MarketData.Domain.Prices;
using MarketData.Domain.Symbols;
using MarketData.UnitTests.Ingestion;
using Microsoft.Extensions.Time.Testing;

namespace MarketData.UnitTests.Prices;

public class PriceAnalyticsServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly SymbolCode Btc = SymbolCode.Parse("BTCUSDT");

    private readonly FakePriceAnalyticsRepository _analytics = new();

    [Theory]
    [InlineData("")]
    [InlineData("AB")]
    [InlineData("BTC-USDT")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTU")]
    public async Task GetAveragePrice24hAsync_SymbolOfInvalidShape_ThrowsInvalidInput(string symbol)
    {
        var service = NewService(monitored: "BTCUSDT");

        await Assert.ThrowsAsync<InvalidInputException>(() => service.GetAveragePrice24hAsync(symbol, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAveragePrice24hAsync_SymbolInLowerCaseWithSpaces_IsNormalised()
    {
        _analytics.Result = AverageOf(Btc);
        var service = NewService(monitored: "BTCUSDT");

        var result = await service.GetAveragePrice24hAsync("  btcusdt ", TestContext.Current.CancellationToken);

        Assert.Equal("BTCUSDT", result.Symbol.Value);
    }

    [Fact]
    public async Task GetAveragePrice24hAsync_UnmonitoredSymbol_ThrowsNotFound()
    {
        var service = NewService(monitored: "ETHUSDT");

        var failure = await Assert.ThrowsAsync<NotFoundException>(() => service.GetAveragePrice24hAsync("BTCUSDT", TestContext.Current.CancellationToken));

        Assert.Contains("not a monitored symbol", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAveragePrice24hAsync_NoCandlesInTheWindow_ThrowsNotFoundInsteadOfReturningZero()
    {
        var service = NewService(monitored: "BTCUSDT");

        var failure = await Assert.ThrowsAsync<NotFoundException>(() => service.GetAveragePrice24hAsync("BTCUSDT", TestContext.Current.CancellationToken));

        Assert.Contains("No data collected yet", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAveragePrice24hAsync_Always_AsksForTheLast24HoursEndingNow()
    {
        _analytics.Result = AverageOf(Btc);
        var service = NewService(monitored: "BTCUSDT");

        await service.GetAveragePrice24hAsync("BTCUSDT", TestContext.Current.CancellationToken);

        Assert.Equal((Now.AddHours(-24), Now), _analytics.RequestedWindow);
    }

    [Fact]
    public async Task GetAveragePrice24hAsync_CandlesFound_ReturnsTheAverageOfTheRepository()
    {
        var average = new AveragePrice(Btc, Now.AddMinutes(-3), Now.AddMinutes(-1), 100.12345678m, 2);
        _analytics.Result = average;
        var service = NewService(monitored: "BTCUSDT");

        var result = await service.GetAveragePrice24hAsync("BTCUSDT", TestContext.Current.CancellationToken);

        Assert.Equal(average, result);
    }

    private static AveragePrice AverageOf(SymbolCode symbol) => new(symbol, Now.AddMinutes(-1), Now, 100m, 1);

    private PriceAnalyticsService NewService(string monitored) =>
        new(new FakeMonitoredSymbols(monitored), _analytics, new FakeTimeProvider(Now));
}
