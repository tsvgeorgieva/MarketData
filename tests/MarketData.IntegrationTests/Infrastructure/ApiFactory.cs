using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace MarketData.IntegrationTests.Infrastructure;

/// <summary>The real API host on a real database, with the clock under test control.</summary>
public sealed class ApiFactory(string connectionString, bool migrateOnStartup = true) : WebApplicationFactory<Program>
{
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:MarketData"] = connectionString,
                ["Database:MigrateOnStartup"] = migrateOnStartup.ToString(),
            }));

        builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(Clock));
    }
}
