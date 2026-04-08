using System.Text.Json;

namespace Chatbot.Infrastructure.Agentic;

internal static class SemanticKernelJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}