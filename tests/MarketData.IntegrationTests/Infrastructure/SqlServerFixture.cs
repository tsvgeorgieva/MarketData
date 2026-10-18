using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace MarketData.IntegrationTests.Infrastructure;

/// <summary>One real SQL Server container for the whole test run; each test gets its own database.</summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private const string Image = "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04";

    private readonly MsSqlContainer _container = new MsSqlBuilder(Image).Build();

    public ValueTask InitializeAsync() => new(_container.StartAsync());

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    public string NewDatabaseConnectionString() =>
        new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = $"MarketData_{Guid.NewGuid():N}",
        }.ConnectionString;
}
