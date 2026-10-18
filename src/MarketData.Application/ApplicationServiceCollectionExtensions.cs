using MarketData.Application.Ingestion;
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

        services.AddSingleton<CandleWriter>();
        services.AddSingleton<IngestionService>();
        return services;
    }
}
