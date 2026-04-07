using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using AppCfg = Chatbot.Application.Configuration;

namespace Chatbot.Infrastructure;

internal static class InfrastructureHttpClientRegistration
{
    public static IServiceCollection AddInfrastructureHttpClients(this IServiceCollection services)
    {
        services.AddHttpClient("OpenAICompatible")
            .ConfigureHttpClient(ConfigureOpenAiCompatibleClient);
        services.AddHttpClient("WebSearch")
            .ConfigureHttpClient(ConfigureWebSearchClient);

        return services;
    }

    private static void ConfigureOpenAiCompatibleClient(IServiceProvider serviceProvider, HttpClient client)
    {
        var options = serviceProvider.GetRequiredService<IOptions<ExternalProviderClientOptions>>().Value;
        client.BaseAddress = new Uri(NormalizeBaseUrl(options.OpenAiCompatibleBaseUrl));
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    }

    private static void ConfigureWebSearchClient(IServiceProvider serviceProvider, HttpClient client)
    {
        var options = serviceProvider.GetRequiredService<IOptions<AppCfg.WebSearchOptions>>().Value;
        client.Timeout = TimeSpan.FromSeconds(Math.Max(2, options.TimeoutSeconds));
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ChatbotRagPlatform/1.0 (+https://localhost)");
    }

    private static string NormalizeBaseUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return baseUrl;
        }

        return baseUrl.EndsWith("/", StringComparison.Ordinal)
            ? baseUrl
            : baseUrl + "/";
    }
}