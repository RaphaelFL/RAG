using System.Text.Json;

namespace Chatbot.Infrastructure.Persistence;

internal static class OperationalAuditJsonSerializer
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}