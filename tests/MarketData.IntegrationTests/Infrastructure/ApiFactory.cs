using MarketData.Domain.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Mvc.Testing;
using Serilog.Core;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace MarketData.IntegrationTests.Infrastructure;

/// <summary>The real API host on a real database, with Binance and the clock under test control.</summary>
public sealed class ApiFactory(
    string connectionString,
    bool migrateOnStartup = true,
    string adminApiKey = ApiFactory.ValidAdminApiKey) : WebApplicationFactory<Program>
{
    public const string ValidAdminApiKey = "test-only-admin-key-0123456789";

    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    public FakeBinance Binance { get; } = new();

    public FakeBinanceSymbolCatalog Catalog { get; } = new();

    public CapturingLogSink Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:MarketData"] = connectionString,
                ["Database:MigrateOnStartup"] = migrateOnStartup.ToString(),
                ["Admin:ApiKey"] = adminApiKey,
            }));

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(Clock);
            services.AddSingleton<IBinanceStream>(Binance);
            services.AddSingleton<IBinanceHistory>(Binance);
            services.AddSingleton<IBinanceSymbolCatalog>(Catalog);
            services.AddSingleton<ILogEventSink>(Logs);
        });
    }
}
