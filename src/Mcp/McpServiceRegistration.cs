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
        services.AddScoped<IMcpToolHandler, SearchToolHandler>();
        services.AddScoped<IMcpToolHandler, RetrieveDocumentChunksToolHandler>();
        services.AddScoped<IMcpToolHandler, SummarizeSourcesToolHandler>();
        services.AddScoped<IMcpToolHandler, ReindexToolHandler>();
        services.AddScoped<IMcpToolHandler, ListTemplatesToolHandler>();
        services.AddScoped<IMcpToolHandler, FileSearchToolHandler>();
        services.AddScoped<IMcpToolHandler, PromptAssemblyToolHandler>();
        services.AddScoped<IMcpToolHandler, EmbeddingGenerateToolHandler>();
        services.AddScoped<IMcpToolHandler, WebSearchToolHandler>();
        services.AddScoped<IMcpToolHandler, CodeInterpreterToolHandler>();
        services.AddScoped<IMcpToolHandler, AgentRunToolHandler>();
        services.AddScoped<IMcpServer, McpServer>();

        return services;
    }
}
