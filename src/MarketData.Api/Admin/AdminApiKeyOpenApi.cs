using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace MarketData.Api.Admin;

/// <summary>Describes the admin key in the OpenAPI document, so Swagger UI offers an Authorize button and sends the header.</summary>
internal static class AdminApiKeyOpenApi
{
    private const string SchemeName = "AdminApiKey";

    public static OpenApiOptions AddAdminApiKey(this OpenApiOptions options)
    {
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = AdminApiKeyAttribute.HeaderName,
                Description = "The admin API key, sent in the X-Api-Key header."
            };
            return Task.CompletedTask;
        });

        options.AddOperationTransformer((operation, context, _) =>
        {
            if (context.Description.ActionDescriptor.EndpointMetadata.OfType<AdminApiKeyAttribute>().Any())
            {
                operation.Security ??= [];
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(SchemeName, context.Document)] = []
                });
            }
            return Task.CompletedTask;
        });

        return options;
    }
}
