using Microsoft.Extensions.DependencyInjection;
using Chatbot.Application.Abstractions;
using Chatbot.Ingestion.Chunking;
using Chatbot.Ingestion.Parsers;

namespace Chatbot.Ingestion;

/// <summary>
/// Registro de serviços de ingestão.
/// Quando os pipelines reais de parsing/chunking/OCR forem implementados,
/// registrá-los aqui.
/// </summary>
public static class IngestionServiceRegistration
{
    public static IServiceCollection AddIngestion(this IServiceCollection services)
    {
        services.AddSingleton<IChunkingStrategy, SlidingWindowChunkingStrategy>();
        services.AddSingleton<IDocumentParser, DirectDocumentParser>();
        services.AddScoped<IDocumentTextExtractor, DocumentTextExtractor>();

        return services;
    }
}
