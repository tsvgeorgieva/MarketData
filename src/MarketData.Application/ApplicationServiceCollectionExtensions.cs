using MarketData.Application.Ingestion;
using MarketData.Application.Prices;
using MarketData.Application.Symbols;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MarketData.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddOptions<SymbolsOptions>()
            .BindConfiguration(SymbolsOptions.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<SymbolsOptions>, SymbolsOptionsValidator>();

        services.AddOptions<IngestionOptions>()
            .BindConfiguration(IngestionOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<CandleQueue>();
        services.AddSingleton<SymbolChangesQueue>();
        services.AddSingleton<ISymbolChangeEventHandler, SymbolChangeEventHandler>();
        services.AddSingleton<BackfillService>();
        services.AddSingleton<ICandleWriterService, CandleWriterService>();
        services.AddSingleton<IIngestionService, IngestionService>();
        services.AddSingleton<IPriceAnalyticsService, PriceAnalyticsService>();
        services.AddSingleton<ISymbolAdministrationService, SymbolAdministrationService>();
        return services;
    }
}
