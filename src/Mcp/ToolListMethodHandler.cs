using System.Security.Claims;

namespace Chatbot.Mcp;

public sealed class ToolListMethodHandler : IMcpMethodHandler
{
    public IReadOnlyCollection<string> SupportedMethods { get; } = new[] { "tools/list" };

    public Task<JsonRpcResponse> HandleAsync(JsonRpcRequest request, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        return Task.FromResult(McpResponseFactory.Ok(request.Id, new
        {
            tools = new[]
            {
                new { name = "search", description = "Alias legado para search_knowledge." },
                new { name = "search_knowledge", description = "Busca conhecimento indexado por tenant." },
                new { name = "retrieve_document_chunks", description = "Recupera chunks relevantes para uma consulta." },
                new { name = "summarize_sources", description = "Resume as fontes recuperadas para uma consulta." },
                new { name = "reindex", description = "Alias legado para reindex_document." },
                new { name = "reindex_document", description = "Reindexa documentos existentes. Requer papel administrativo." },
                new { name = "list_templates", description = "Lista templates versionados disponiveis." },
                new { name = "file_search", description = "Busca agentic em arquivos internos usando retrieval governado." },
                new { name = "assemble_prompt", description = "Monta prompt final grounded com contexto e citacoes." },
                new { name = "embedding_generate", description = "Gera embeddings pela capacidade interna da plataforma." },
                new { name = "web_search", description = "Executa busca web pela tool configurada." },
                new { name = "code_interpreter", description = "Executa codigo em interpretador controlado." },
                new { name = "agent_run", description = "Executa um agent governado com budget e timeout." }
            }
        }));
    }
}