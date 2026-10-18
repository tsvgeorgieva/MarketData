using MarketData.Domain.Interfaces;
using MarketData.Infrastructure.Binance;
using MarketData.Infrastructure.Persistence;
using MarketData.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MarketData.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddOptions<DatabaseOptions>()
            .BindConfiguration(DatabaseOptions.SectionName)
            .Configure<IConfiguration>((options, configuration) =>
                options.ConnectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionStringName) ?? "")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<BinanceOptions>()
            .BindConfiguration(BinanceOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContextFactory<MarketDataDbContext>((provider, builder) =>
            builder.UseSqlServer(provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString));

        services.AddSingleton<ICandleRepository, CandleRepository>();
        services.AddSingleton<IMonitoredSymbolRepository, MonitoredSymbolRepository>();
        services.AddSingleton<ICandlePriceAnalyticsRepository, CandlePriceAnalyticsRepository>();
        services.AddSingleton<IBinanceStreamFactory, BinanceStreamFactory>();

        services.AddSingleton<DatabaseSeeder>();
        services.AddHostedService<DatabaseInitializer>();
        return services;
    }
}
