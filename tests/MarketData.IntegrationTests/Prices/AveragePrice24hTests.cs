using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using MarketData.Api.Contracts;
using MarketData.Domain.Candles;
using MarketData.Domain.Interfaces;
using MarketData.Domain.Symbols;
using MarketData.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace MarketData.IntegrationTests.Prices;

public partial class AveragePrice24hTests(SqlServerFixture sql)
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Get_CandlesInTheLast24Hours_ReturnsMeanWindowAndCount()
    {
        await using var factory = NewFactory(out _);
        using var client = factory.CreateClient();
        await StoreAsync(factory, At(-3, 100m), At(-2, 101m), At(-1, 102m));

        var response = await client.GetFromJsonAsync<AveragePriceResponse>("/api/BTCUSDT/24hAvgPrice", TestContext.Current.CancellationToken);

        Assert.Equal(
            ("BTCUSDT", Now.UtcDateTime.AddMinutes(-3), Now.UtcDateTime, 101m, 3),
            (response!.Symbol, response.From, response.To, response.Average, response.DataPoints));
    }

    [Fact]
    public async Task Get_CandlesAtTheEdgesOfTheWindow_IncludesTheStartAndExcludesTheEnd()
    {
        await using var factory = NewFactory(out _);
        using var client = factory.CreateClient();
        await StoreAsync(
            factory,
            At(minutesFromNow: -24 * 60 - 1, close: 1000m),
            At(minutesFromNow: -24 * 60, close: 10m),
            At(minutesFromNow: -1, close: 20m),
            At(minutesFromNow: 0, close: 1000m));

        var response = await client.GetFromJsonAsync<AveragePriceResponse>("/api/BTCUSDT/24hAvgPrice", TestContext.Current.CancellationToken);

        Assert.Equal((15m, 2), (response!.Average, response.DataPoints));
    }

    [Fact]
    public async Task Get_MeanBeyondEightPlaces_IsCutOffAtEightPlacesByTheDatabase()
    {
        await using var factory = NewFactory(out _);
        using var client = factory.CreateClient();
        await StoreAsync(factory, At(-3, 1m), At(-2, 1m), At(-1, 0m));

        var response = await client.GetFromJsonAsync<AveragePriceResponse>("/api/BTCUSDT/24hAvgPrice", TestContext.Current.CancellationToken);

        Assert.Equal(0.66666666m, response!.Average);
    }

    [Fact]
    public async Task Get_SymbolInLowerCaseWithSpaces_IsAcceptedAndNormalised()
    {
        await using var factory = NewFactory(out _);
        using var client = factory.CreateClient();
        await StoreAsync(factory, At(-1, 100m));

        var response = await client.GetFromJsonAsync<AveragePriceResponse>("/api/%20btcusdt%20/24hAvgPrice", TestContext.Current.CancellationToken);

        Assert.Equal("BTCUSDT", response!.Symbol);
    }

    [Theory]
    [InlineData("AB")]
    [InlineData("BTC-USDT")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTU")]
    public async Task Get_SymbolWithInvalidShape_ReturnsBadRequestProblem(string symbol)
    {
        await using var factory = NewFactory(out _);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/{symbol}/24hAvgPrice", TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_SymbolThatIsNotMonitored_ReturnsNotFoundProblem()
    {
        await using var factory = NewFactory(out _);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/XRPUSDT/24hAvgPrice", TestContext.Current.CancellationToken);

        var problem = await AssertProblemAsync(response, HttpStatusCode.NotFound);
        Assert.Contains("not a monitored symbol", problem.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_RemovedSymbol_IsTreatedAsUnknown()
    {
        await using var factory = NewFactory(out var connectionString);
        using var client = factory.CreateClient();
        await StoreAsync(factory, At(-1, 100m));
        await TestDatabase.ExecuteAsync(connectionString, "UPDATE MonitoredSymbols SET RemovedAt = SYSUTCDATETIME() WHERE Symbol = 'BTCUSDT'");

        var response = await client.GetAsync("/api/BTCUSDT/24hAvgPrice", TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_MonitoredSymbolWithoutCandles_ReturnsNotFoundInsteadOfZero()
    {
        await using var factory = NewFactory(out _);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/BTCUSDT/24hAvgPrice", TestContext.Current.CancellationToken);

        var problem = await AssertProblemAsync(response, HttpStatusCode.NotFound);
        Assert.Contains("No data collected yet", problem.Detail, StringComparison.Ordinal);
    }

    private ApiFactory NewFactory(out string connectionString)
    {
        connectionString = sql.NewDatabaseConnectionString();
        var factory = new ApiFactory(connectionString);
        factory.Clock.SetUtcNow(Now);
        return factory;
    }

    private static Candle At(int minutesFromNow, decimal close) =>
        new(SymbolCode.Parse("BTCUSDT"), Now.AddMinutes(minutesFromNow), close, close, close, close, 1m);

    private static Task<int> StoreAsync(ApiFactory factory, params Candle[] candles) =>
        factory.Services.GetRequiredService<ICandleRepository>().InsertMissingAsync(candles, TestContext.Current.CancellationToken);

    /// <summary>Every error is an RFC 9457 problem document with the status and the trace id of the request.</summary>
    private static async Task<ProblemDetails> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode expectedStatus)
    {
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);

        Assert.Equal(
            (expectedStatus, "application/problem+json", (int)expectedStatus, true),
            (response.StatusCode, response.Content.Headers.ContentType?.MediaType, problem!.Status, TraceIdPattern().IsMatch(problem.Extensions["traceId"]?.ToString() ?? "")));
        return problem;
    }

    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex TraceIdPattern();
}
