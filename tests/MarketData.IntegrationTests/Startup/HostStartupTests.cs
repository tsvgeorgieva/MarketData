using System.Net;
using MarketData.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace MarketData.IntegrationTests.Startup;

public class HostStartupTests(SqlServerFixture sql)
{
    [Fact]
    public async Task LiveEndpoint_HostRunning_ReturnsHealthy()
    {
        await using var factory = new ApiFactory(sql.NewDatabaseConnectionString());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Startup_MigrationOn_CreatesSchemaInNewDatabase()
    {
        var connectionString = sql.NewDatabaseConnectionString();
        await using var factory = new ApiFactory(connectionString, migrateOnStartup: true);
        using var client = factory.CreateClient();

        var tables = await TestDatabase.ScalarAsync<int>(
            connectionString,
            "SELECT COUNT(*) FROM sys.tables WHERE name IN ('Candles', 'MonitoredSymbols')");

        Assert.Equal(2, tables);
    }

    [Fact]
    public async Task Startup_MigrationOffAndSchemaMissing_StopsHost()
    {
        var connectionString = sql.NewDatabaseConnectionString();
        var catalog = TestDatabase.CatalogOf(connectionString);
        await TestDatabase.ExecuteAsync(TestDatabase.WithCatalog(connectionString, "master"), $"CREATE DATABASE [{catalog}]");
        await using var factory = new ApiFactory(connectionString, migrateOnStartup: false);

        await Assert.ThrowsAsync<SqlException>(() => Task.Run(() => factory.CreateClient()));
    }

    [Fact]
    public async Task Startup_ConnectionStringMissing_StopsHost()
    {
        await using var factory = new ApiFactory(connectionString: "");

        await Assert.ThrowsAsync<OptionsValidationException>(() => Task.Run(() => factory.CreateClient()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("too-short-key")]
    public async Task Startup_AdminKeyMissingOrTooShort_StopsHost(string adminApiKey)
    {
        await using var factory = new ApiFactory(sql.NewDatabaseConnectionString(), adminApiKey: adminApiKey);

        await Assert.ThrowsAsync<OptionsValidationException>(() => Task.Run(() => factory.CreateClient()));
    }

    [Fact]
    public async Task Logs_AfterStartupAndRequest_DoNotContainSecrets()
    {
        var connectionString = sql.NewDatabaseConnectionString();
        await using var factory = new ApiFactory(connectionString);
        using var client = factory.CreateClient();
        await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        var leaked = factory.Logs.Messages.Where(message =>
            message.Contains(ApiFactory.ValidAdminApiKey, StringComparison.Ordinal) ||
            message.Contains(connectionString, StringComparison.Ordinal));

        Assert.Empty(leaked);
    }
}
