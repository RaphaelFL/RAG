using Microsoft.Extensions.DependencyInjection;

namespace Chatbot.Mcp;

/// <summary>
/// Registro de serviços MCP (Model Context Protocol).
/// Resources, Tools e Prompts expostos como superfície MCP.
/// Habilitado via feature flag "EnableMcp".
/// </summary>
public static class McpServiceRegistration
{
    public static IServiceCollection AddMcp(this IServiceCollection services)
    {
        services.AddScoped<IMcpServer, McpServer>();

        return services;
    }
}
