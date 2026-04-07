using Chatbot.Api.Bootstrap;
using Chatbot.Api.Contracts;
using Chatbot.Application;
using Chatbot.Infrastructure;
using Chatbot.Ingestion;
using Chatbot.Mcp;
using Chatbot.Retrieval;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddOptionalLocalConfiguration();

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore.DataProtection", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.File("logs/chatbot-.txt", rollingInterval: RollingInterval.Day)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Chatbot.Api")
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddApiPresentation(builder.Environment, builder.Configuration);
builder.Services.AddApiOptions(builder.Configuration);
builder.Services.AddApiAuthentication(builder);
builder.Services.AddApiRateLimiting();

builder.Services
    .AddApplication()
    .AddInfrastructure()
    .AddIngestion()
    .AddRetrieval()
    .AddMcp();

builder.Services.AddApiTelemetry();

// Add logging
builder.Services.AddLogging();

var app = builder.Build();

app.ConfigureApiPipeline();

try
{
    Log.Information("Starting Chatbot API");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
