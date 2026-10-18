using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MarketData.Client;
using MarketData.Domain.Candles;
using MarketData.Domain.Interfaces;
using MarketData.Domain.Symbols;
using MarketData.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace MarketData.IntegrationTests.Client;

public class ConsoleClientTests(SqlServerFixture sql)
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 12, 3, 0, TimeSpan.Zero);

    [Fact]
    public async Task Avg24h_CandlesStored_PrintsReadableTextAndExitsWithZero()
    {
        await using var factory = NewFactory();
        await StoreAsync(factory, At(11, 58, 100m), At(11, 59, 102m));

        var run = await RunAsync(factory, "avg24h", "BTCUSDT", "--url", "http://localhost");

        Assert.Equal(
            (0, "BTCUSDT 24h average price: 101\nwindow: 2026-01-02 11:58:00Z to 2026-01-02 12:00:00Z\ndata points: 2\n", ""),
            (run.ExitCode, run.Out, run.Error));
    }

    [Fact]
    public async Task Sma_WithEnd_PrintsReadableTextAndFlagsTheFormingPeriod()
    {
        await using var factory = NewFactory();
        await StoreAsync(factory, At(11, 55, 30m), At(12, 0, 40m));

        var run = await RunAsync(factory, "sma", "BTCUSDT", "-n", "2", "-p", "5m", "-e", "2026-01-02T12:01:00", "--url", "http://localhost");

        Assert.Equal(
            (0, "BTCUSDT simple moving average over 2 x 5m: 35\nwindow: 2026-01-02 11:55:00Z to 2026-01-02 12:01:00Z\ndata points: 2\nthe newest period has not finished yet\n", ""),
            (run.ExitCode, run.Out, run.Error));
    }

    [Fact]
    public async Task Avg24h_FormatJson_PrintsTheApiJsonUnchanged()
    {
        await using var factory = NewFactory();
        await StoreAsync(factory, At(11, 59, 100m));

        var run = await RunAsync(factory, "avg24h", "BTCUSDT", "--format", "json");

        using var document = JsonDocument.Parse(run.Out);
        Assert.Equal((0, "BTCUSDT", 100m), (run.ExitCode, document.RootElement.GetProperty("symbol").GetString(), document.RootElement.GetProperty("average").GetDecimal()));
    }

    [Fact]
    public async Task Sma_FormatXml_PrintsTheApiXmlUnchanged()
    {
        await using var factory = NewFactory();
        await StoreAsync(factory, At(12, 2, 100m));

        var run = await RunAsync(factory, "sma", "BTCUSDT", "-n", "1", "-p", "1m", "--format", "xml");

        var root = XElement.Parse(run.Out);
        Assert.Equal((0, "simpleMovingAverage", "100"), (run.ExitCode, root.Name.LocalName, root.Element("average")!.Value.TrimEnd('0').TrimEnd('.')));
    }

    [Fact]
    public async Task Avg24h_BaseUrlFromEnvironmentVariable_IsUsedWhenNoFlagIsGiven()
    {
        await using var factory = NewFactory();
        await StoreAsync(factory, At(11, 59, 100m));

        var run = await RunAsync(factory, name => name == "MARKETDATA_URL" ? "http://localhost" : null, "avg24h", "BTCUSDT");

        Assert.Equal(0, run.ExitCode);
    }

    [Theory]
    [InlineData("text")]
    [InlineData("json")]
    [InlineData("xml")]
    public async Task Avg24h_NoData_WritesOneLineWithStatusAndTraceIdToStderrAndExitsWithOne(string format)
    {
        await using var factory = NewFactory();

        var run = await RunAsync(factory, "avg24h", "BTCUSDT", "--format", format);

        Assert.Equal((1, "", true), (run.ExitCode, run.Out, Regex.IsMatch(run.Error, @"^error: 404 Not Found(: .+)? \(trace id [0-9a-f]{32}\)\n$")));
    }

    [Fact]
    public async Task Sma_InvalidPeriod_ReportsTheApiValidationErrorWithExitCodeOne()
    {
        await using var factory = NewFactory();

        var run = await RunAsync(factory, "sma", "BTCUSDT", "-n", "3", "-p", "2h");

        Assert.Equal((1, "", true), (run.ExitCode, run.Out, run.Error.StartsWith("error: 400 Bad Request", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("avg24h")]
    [InlineData("nope BTCUSDT")]
    [InlineData("sma BTCUSDT -p 5m")]
    [InlineData("sma BTCUSDT -n 3")]
    [InlineData("sma BTCUSDT -n")]
    [InlineData("avg24h BTCUSDT -n 3")]
    [InlineData("avg24h BTCUSDT --bogus 1")]
    [InlineData("avg24h BTCUSDT --format yaml")]
    [InlineData("avg24h BTCUSDT --url ftp://x")]
    public async Task BadUsage_WritesTheErrorAndUsageToStderrAndExitsWithTwo(string commandLine)
    {
        await using var factory = NewFactory();

        var run = await RunAsync(factory, commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        Assert.Equal((2, "", true), (run.ExitCode, run.Out, run.Error.StartsWith("error: ", StringComparison.Ordinal) && run.Error.Contains("usage:", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Avg24h_ServerNotListening_ExitsWithThree()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await ConsoleClient.RunAsync(["avg24h", "BTCUSDT", "--url", "http://127.0.0.1:1"], output, error, _ => null, null, TestContext.Current.CancellationToken);

        Assert.Equal((3, "", true), (exitCode, output.ToString(), error.ToString().StartsWith("error: cannot reach http://127.0.0.1:1/", StringComparison.Ordinal)));
    }

    private ApiFactory NewFactory()
    {
        var factory = new ApiFactory(sql.NewDatabaseConnectionString());
        factory.Clock.SetUtcNow(Now);
        return factory;
    }

    private static Task<ClientRun> RunAsync(ApiFactory factory, params string[] args) => RunAsync(factory, _ => null, args);

    private static async Task<ClientRun> RunAsync(ApiFactory factory, Func<string, string?> getEnvironmentVariable, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = await ConsoleClient.RunAsync(args, output, error, getEnvironmentVariable, factory.Server.CreateHandler(), TestContext.Current.CancellationToken);
        return new ClientRun(exitCode, output.ToString().ReplaceLineEndings("\n"), error.ToString().ReplaceLineEndings("\n"));
    }

    private static Candle At(int hour, int minute, decimal close = 1m) =>
        new(SymbolCode.Parse("BTCUSDT"), new DateTimeOffset(2026, 1, 2, hour, minute, 0, TimeSpan.Zero), close, close, close, close, 1m);

    private static Task<int> StoreAsync(ApiFactory factory, params Candle[] candles) =>
        factory.Services.GetRequiredService<ICandleRepository>().InsertMissingAsync(candles, TestContext.Current.CancellationToken);

    private sealed record ClientRun(int ExitCode, string Out, string Error);
}
