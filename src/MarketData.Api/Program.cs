using System.Diagnostics;
using MarketData.Api.Admin;
using MarketData.Api.Errors;
using MarketData.Api.Ingestion;
using MarketData.Application;
using MarketData.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Formatting.Compact;

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
builder.Services.AddOpenApi();

// Every error is a problem-details document carrying the same trace id as the log lines of the request.
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// Hosted services start in this order and stop in reverse: the database is ready before anything runs,
// and on shutdown the feed stops first so the writer can save what is still queued.
builder.Services.AddHostedService<CandleWriterHostedService>();
builder.Services.AddHostedService<IngestionHostedService>();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapControllers();
app.MapOpenApi();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

app.Run();

public partial class Program;
