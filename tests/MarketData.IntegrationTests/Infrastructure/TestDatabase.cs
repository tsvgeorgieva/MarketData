using Microsoft.Data.SqlClient;

namespace MarketData.IntegrationTests.Infrastructure;

public static class TestDatabase
{
    public static async Task<T> ScalarAsync<T>(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new SqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    public static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    public static string WithCatalog(string connectionString, string catalog) =>
        new SqlConnectionStringBuilder(connectionString) { InitialCatalog = catalog }.ConnectionString;

    public static string CatalogOf(string connectionString) =>
        new SqlConnectionStringBuilder(connectionString).InitialCatalog;
}
