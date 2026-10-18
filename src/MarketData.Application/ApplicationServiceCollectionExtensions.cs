using MarketData.Application.Ingestion;
using MarketData.Application.Ingestion.Interfaces;
using MarketData.Application.Prices.Interfaces;
using MarketData.Application.Prices;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using MarketData.Application.Symbols.Interfaces;
using MarketData.Application.Symbols;

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

        services.AddSingleton<CandleIngestionQueue>();
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
