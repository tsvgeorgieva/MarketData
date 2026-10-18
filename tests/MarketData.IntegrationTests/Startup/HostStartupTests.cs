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
    public async Task Startup_MigrationOn_AppliesMigrationsToNewDatabase()
    {
        var connectionString = sql.NewDatabaseConnectionString();
        await using var factory = new ApiFactory(connectionString, migrateOnStartup: true);
        using var client = factory.CreateClient();

        var applied = await ScalarAsync<int>(connectionString, "SELECT COUNT(*) FROM __EFMigrationsHistory");

        Assert.Equal(1, applied);
    }

    [Fact]
    public async Task Startup_MigrationOff_LeavesDatabaseUncreated()
    {
        var connectionString = sql.NewDatabaseConnectionString();
        await using var factory = new ApiFactory(connectionString, migrateOnStartup: false);
        using var client = factory.CreateClient();
        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        var master = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" }.ConnectionString;

        var exists = await ScalarAsync<int>(master, $"SELECT COUNT(*) FROM sys.databases WHERE name = '{databaseName}'");

        Assert.Equal(0, exists);
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

    private static async Task<T> ScalarAsync<T>(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new SqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}
