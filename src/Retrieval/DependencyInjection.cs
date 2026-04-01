using Microsoft.Extensions.DependencyInjection;
using Chatbot.Application.Abstractions;
using Chatbot.Retrieval.Citations;

namespace Chatbot.Retrieval;

/// <summary>
/// Registro de serviços de retrieval.
/// Quando a camada de retrieval ganhar implementações próprias (ranking, citations builder),
/// registrá-las aqui.
/// </summary>
public static class RetrievalServiceRegistration
{
    public static IServiceCollection AddRetrieval(this IServiceCollection services)
    {
        services.AddSingleton<ICitationAssembler, CitationAssembler>();

        return services;
    }
}
