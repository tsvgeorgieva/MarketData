using System.Diagnostics;
using MarketData.Api.Admin;
using MarketData.Api.Ingestion;
using MarketData.Api.Errors;
using MarketData.Infrastructure;
using MarketData.Application;
using Serilog.Formatting.Compact;
using Serilog;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(new RenderedCompactJsonFormatter()));

builder.Services.AddOptions<AdminOptions>()
    .BindConfiguration(AdminOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddHealthChecks();

// JSON is the first formatter and so the default; XML is served only when asked for, anything else is a 406.
builder.Services.AddControllers(options =>
    {
        options.ReturnHttpNotAcceptable = true;
    })
    .AddXmlSerializerFormatters();
builder.Services.AddOpenApi(options => options.AddAdminApiKey());

// Every error is a problem-details document carrying the same trace id as the log lines of the request.
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// Hosted services start in this order and stop in reverse: the database is ready before anything runs,
// and on shutdown the feed stops first so the writer can save what is still queued.
// Hosted services start one by one in this order: the ingestion first, then the ones that work with its stream and queue.
builder.Services.AddHostedService<IngestionHostedService>();
builder.Services.AddHostedService<CandleWriterHostedService>();
builder.Services.AddHostedService<SymbolChangeHostedService>();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapControllers();
app.MapOpenApi();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "MarketData v1"));
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

app.Run();

public partial class Program;
