using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MarketData.Api.Contracts;
using MarketData.Domain.Candles;
using MarketData.Domain.Interfaces;
using MarketData.Domain.Symbols;
using MarketData.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace MarketData.IntegrationTests.Prices;

public class SimpleMovingAverageTests(SqlServerFixture sql)
{
    // A Friday; the week began on Monday 29 December.
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 12, 3, 0, TimeSpan.Zero);

    [Fact]
    public async Task Get_NewestPeriodStillForming_IsIncludedFromTheLatestClosedCandleAndFlagged()
    {
        await using var factory = NewFactory();
        using var client = factory.CreateClient();
        await StoreAsync(factory, At(11, 55, 30m), At(12, 0, 40m), At(12, 2, 50m), At(12, 3, 999m));

        var response = await GetAsync(client, "n=2&p=5m");

        // The 12:03 Candle has not closed yet at 12:03, so the 12:00 period is represented by the 12:02 Candle.
        Assert.Equal(
            ("BTCUSDT", "5m", 2, At(11, 55).OpenTime.UtcDateTime, At(12, 3).OpenTime.UtcDateTime, 40m, 2, true),
            (response.Symbol, response.Period, response.Requested, response.From, response.To, response.Average, response.DataPoints, response.InProgress));
    }

    [Fact]
    public async Task Get_PeriodsWithoutData_AreSkippedAndShownAsFewerDataPointsThanRequested()
    {
        await using var factory = NewFactory();
        using var client = factory.CreateClient();
        await StoreAsync(factory, At(11, 50, 10m), At(11, 54, 20m), At(11, 55, 30m), At(12, 2, 50m));

        var response = await GetAsync(client, "n=4&p=5m");

        Assert.Equal((33.33333333m, 3, 4), (response.Average, response.DataPoints, response.Requested));
    }

    [Theory]
    [InlineData("2026-01-02T11:56:00")]
    [InlineData("2026-01-02T11:56")]
    [InlineData("2026-01-02T11:56:00.000")]
    public async Task Get_EndInThePast_IgnoresLaterCandles(string end)
    {
        await using var factory = NewFactory();
        using var client = factory.CreateClient();
        await StoreAsync(factory, At(11, 54, 20m), At(11, 55, 30m), At(12, 2, 50m));

        var response = await GetAsync(client, $"n=1&p=5m&e={end}");

        // The last minute closed by 11:56 is 11:55, so the newest period is 11:55 to 12:00, still forming.
        Assert.Equal((30m, 1, true), (response.Average, response.DataPoints, response.InProgress));
    }

    [Fact]
    public async Task Get_EndInTheFuture_IsTreatedAsNow()
    {
        await using var factory = NewFactory();
        using var client = factory.CreateClient();
        await StoreAsync(factory, At(12, 2, 50m));

        var future = await GetAsync(client, "n=1&p=5m&e=2030-01-01");
        var now = await GetAsync(client, "n=1&p=5m");

        Assert.Equal(now.Average, future.Average);
    }

    [Fact]
    public async Task Get_OneMinutePeriod_IsNeverInProgress()
    {
        await using var factory = NewFactory();
        using var client = factory.CreateClient();
        await StoreAsync(factory, At(12, 1, 10m), At(12, 2, 20m));

        var response = await GetAsync(client, "n=2&p=1m");

        Assert.Equal((15m, 2, false), (response.Average, response.DataPoints, response.InProgress));
    }

    [Fact]
    public async Task Get_WeeklyPeriod_StartsOnMondayAtMidnightUtc()
    {
        await using var factory = NewFactory();
        using var client = factory.CreateClient();
        await StoreAsync(
            factory,
            new Candle(SymbolCode.Parse("BTCUSDT"), new(2025, 12, 28, 23, 59, 0, TimeSpan.Zero), 100m, 100m, 100m, 100m, 1m),
            new Candle(SymbolCode.Parse("BTCUSDT"), new(2025, 12, 29, 0, 0, 0, TimeSpan.Zero), 200m, 200m, 200m, 200m, 1m),
            new Candle(SymbolCode.Parse("BTCUSDT"), new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero), 300m, 300m, 300m, 300m, 1m));

        var response = await GetAsync(client, "n=2&p=1w");

        Assert.Equal(
            (new DateTime(2025, 12, 22, 0, 0, 0, DateTimeKind.Utc), 200m, 2, true),
            (response.From, response.Average, response.DataPoints, response.InProgress));
    }

    [Fact]
    public async Task Get_DailyPeriod_StartsAtMidnightUtc()
    {
        await using var factory = NewFactory();
        using var client = factory.CreateClient();
        await StoreAsync(
            factory,
            new Candle(SymbolCode.Parse("BTCUSDT"), new(2026, 1, 1, 23, 59, 0, TimeSpan.Zero), 10m, 10m, 10m, 10m, 1m),
            new Candle(SymbolCode.Parse("BTCUSDT"), new(2026, 1, 2, 0, 0, 0, TimeSpan.Zero), 20m, 20m, 20m, 20m, 1m),
            new Candle(SymbolCode.Parse("BTCUSDT"), new(2026, 1, 2, 12, 2, 0, TimeSpan.Zero), 40m, 40m, 40m, 40m, 1m));

        var response = await GetAsync(client, "n=2&p=1d");

        Assert.Equal((25m, 2), (response.Average, response.DataPoints));
    }

    [Fact]
    public async Task Get_SameEnd_AlwaysGivesTheSameAnswerEvenAsTimePasses()
    {
        await using var factory = NewFactory();
        using var client = factory.CreateClient();
        await StoreAsync(factory, At(11, 55, 30m), At(12, 2, 50m));

        var first = await GetAsync(client, "n=3&p=5m&e=2026-01-02T12:01:00");
        factory.Clock.Advance(TimeSpan.FromHours(3));
        await StoreAsync(factory, At(14, 59, 999m));
        var second = await GetAsync(client, "n=3&p=5m&e=2026-01-02T12:01:00");

        Assert.Equivalent(first, second);
    }

    [Fact]
    public async Task Get_AcceptXml_CarriesTheSameContentAsJson()
    {
        await using var factory = NewFactory();
        using var client = factory.CreateClient();
        await StoreAsync(factory, At(12, 2, 50m));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/BTCUSDT/SimpleMovingAverage?n=1&p=5m");
        request.Headers.TryAddWithoutValidation("Accept", "application/xml");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        var values = XElement.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .Elements().ToDictionary(element => element.Name.LocalName, element => element.Value);
        Assert.Equal(
            ("application/xml", 50m, "5m", "true", "2026-01-02T12:00:00Z"),
            (response.Content.Headers.ContentType?.MediaType, decimal.Parse(values["average"], CultureInfo.InvariantCulture), values["period"], values["inProgress"], values["from"]));
    }

    [Fact]
    public async Task Get_NoCandlesInTheWindow_ReturnsNotFoundInsteadOfZero()
    {
        await using var factory = NewFactory();
        using var client = factory.CreateClient();
        await StoreAsync(factory, At(9, 0, 10m));

        var response = await client.GetAsync("/api/BTCUSDT/SimpleMovingAverage?n=2&p=5m", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_SymbolThatIsNotMonitored_ReturnsNotFound()
    {
        await using var factory = NewFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/XRPUSDT/SimpleMovingAverage?n=2&p=5m", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("p=5m", "n")]
    [InlineData("n=0&p=5m", "n")]
    [InlineData("n=1001&p=5m", "n")]
    [InlineData("n=abc&p=5m", "n")]
    [InlineData("n=-3&p=5m", "n")]
    [InlineData("n=2", "p")]
    [InlineData("n=2&p=5M", "p")]
    [InlineData("n=2&p=1h", "p")]
    [InlineData("n=2&p=5m&e=yesterday", "e")]
    public async Task Get_InvalidParameter_ReturnsBadRequestNamingIt(string query, string parameter)
    {
        await using var factory = NewFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/BTCUSDT/SimpleMovingAverage?{query}", TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            (HttpStatusCode.BadRequest, true),
            (response.StatusCode, Regex.IsMatch(body, $"['\"]{parameter}['\"]", RegexOptions.IgnoreCase)));
    }

    private ApiFactory NewFactory()
    {
        var factory = new ApiFactory(sql.NewDatabaseConnectionString());
        factory.Clock.SetUtcNow(Now);
        return factory;
    }

    private static Task<SimpleMovingAverageResponse> GetAsync(HttpClient client, string query) =>
        client.GetFromJsonAsync<SimpleMovingAverageResponse>($"/api/BTCUSDT/SimpleMovingAverage?{query}", TestContext.Current.CancellationToken)!;

    private static Candle At(int hour, int minute, decimal close = 1m) =>
        new(SymbolCode.Parse("BTCUSDT"), new DateTimeOffset(2026, 1, 2, hour, minute, 0, TimeSpan.Zero), close, close, close, close, 1m);

    private static Task<int> StoreAsync(ApiFactory factory, params Candle[] candles) =>
        factory.Services.GetRequiredService<ICandleRepository>().InsertMissingAsync(candles, TestContext.Current.CancellationToken);
}
