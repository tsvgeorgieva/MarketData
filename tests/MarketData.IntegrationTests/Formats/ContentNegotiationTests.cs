using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Xml.Linq;
using MarketData.Domain.Candles;
using MarketData.Domain.Interfaces;
using MarketData.Domain.Symbols;
using MarketData.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace MarketData.IntegrationTests.Formats;

public class ContentNegotiationTests(SqlServerFixture sql)
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Get_AcceptXml_ReturnsTheSameContentAsJsonWithCamelCaseNamesAndUtcTimestamps()
    {
        await using var factory = NewFactory();
        using var client = factory.CreateClient();
        await StoreAsync(factory, At(-3, 100m), At(-1, 102m));

        var json = await GetAsync(client, "application/json");
        var xml = await GetAsync(client, "application/xml");

        var root = XElement.Parse(await xml.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using var document = JsonDocument.Parse(await json.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var fromJson = document.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ToString());
        var fromXml = root.Elements().ToDictionary(e => e.Name.LocalName, e => e.Value);

        Assert.Equal(
            ("application/json", "application/xml", "averagePrice", "2026-01-02T11:57:00Z"),
            (json.Content.Headers.ContentType?.MediaType, xml.Content.Headers.ContentType?.MediaType, root.Name.LocalName, fromXml["from"]));
        Assert.Equal(
            fromJson.OrderBy(p => p.Key, StringComparer.Ordinal),
            fromXml.OrderBy(p => p.Key, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("*/*")]
    [InlineData("application/xml;q=0.5, application/json")]
    public async Task Get_NoPreferenceForXml_ReturnsJson(string? accept)
    {
        await using var factory = NewFactory();
        using var client = factory.CreateClient();
        await StoreAsync(factory, At(-1, 100m));

        var response = await GetAsync(client, accept);

        Assert.Equal(
            (HttpStatusCode.OK, "application/json"),
            (response.StatusCode, response.Content.Headers.ContentType?.MediaType));
    }

    [Fact]
    public async Task Get_AcceptTextXml_ReturnsXml()
    {
        await using var factory = NewFactory();
        using var client = factory.CreateClient();
        await StoreAsync(factory, At(-1, 100m));

        var response = await GetAsync(client, "text/xml");

        Assert.Equal(
            (HttpStatusCode.OK, "text/xml"),
            (response.StatusCode, response.Content.Headers.ContentType?.MediaType));
    }

    [Theory]
    [InlineData("application/yaml")]
    [InlineData("text/html")]
    public async Task Get_UnsupportedFormat_ReturnsNotAcceptable(string accept)
    {
        await using var factory = NewFactory();
        using var client = factory.CreateClient();
        await StoreAsync(factory, At(-1, 100m));

        var response = await GetAsync(client, accept);

        Assert.Equal(HttpStatusCode.NotAcceptable, response.StatusCode);
    }

    [Fact]
    public async Task Get_AcceptXmlAndTheSymbolIsUnknown_ReturnsNotFound()
    {
        await using var factory = NewFactory();
        using var client = factory.CreateClient();

        var response = await GetAsync(client, "application/xml", "XRPUSDT");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_OpenApiDescription_IsServedAndListsTheEndpoint()
    {
        await using var factory = NewFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            (HttpStatusCode.OK, true),
            (response.StatusCode, body.Contains("/api/{symbol}/24hAvgPrice", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Get_OpenApiDescription_DeclaresTheAdminApiKeyHeader()
    {
        await using var factory = NewFactory();
        using var client = factory.CreateClient();

        var body = await client.GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        using var document = System.Text.Json.JsonDocument.Parse(body);
        var scheme = document.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("AdminApiKey");
        var secured = document.RootElement.GetProperty("paths").GetProperty("/api/admin/symbols").GetProperty("get").TryGetProperty("security", out _);
        Assert.Equal(
            ("apiKey", "header", "X-Api-Key", true),
            (scheme.GetProperty("type").GetString(), scheme.GetProperty("in").GetString(), scheme.GetProperty("name").GetString(), secured));
    }

    [Fact]
    public async Task Get_SwaggerUi_IsServed()
    {
        await using var factory = NewFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/index.html", TestContext.Current.CancellationToken);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            (HttpStatusCode.OK, true),
            (response.StatusCode, body.Contains("Swagger UI", StringComparison.Ordinal)));
    }

    private ApiFactory NewFactory()
    {
        var factory = new ApiFactory(sql.NewDatabaseConnectionString());
        factory.Clock.SetUtcNow(Now);
        return factory;
    }

    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string? accept, string symbol = "BTCUSDT")
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/{symbol}/24hAvgPrice");
        if (accept is not null)
        {
            request.Headers.TryAddWithoutValidation("Accept", accept);
        }

        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static Candle At(int minutesFromNow, decimal close) =>
        new(SymbolCode.Parse("BTCUSDT"), Now.AddMinutes(minutesFromNow), close, close, close, close, 1m);

    private static Task<int> StoreAsync(ApiFactory factory, params Candle[] candles) =>
        factory.Services.GetRequiredService<ICandleRepository>().InsertMissingAsync(candles, TestContext.Current.CancellationToken);
}
