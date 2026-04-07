using System.Security.Claims;
using System.Text.Json;

namespace Chatbot.Mcp;

public static class McpRequestHelpers
{
    public static JsonElement GetArguments(JsonElement root)
    {
        return root.TryGetProperty("arguments", out var arguments)
            ? arguments
            : root;
    }

    public static Guid? GetTenantId(ClaimsPrincipal user)
    {
        var tenantClaim = user.FindFirst("tenant_id")?.Value;
        return Guid.TryParse(tenantClaim, out var tenantId) ? tenantId : null;
    }

    public static string ReadString(JsonElement arguments, string propertyName, string defaultValue = "")
    {
        return arguments.TryGetProperty(propertyName, out var element)
            ? element.GetString() ?? defaultValue
            : defaultValue;
    }

    public static int ReadInt(JsonElement arguments, string propertyName, int defaultValue)
    {
        return arguments.TryGetProperty(propertyName, out var element) && element.TryGetInt32(out var parsed)
            ? parsed
            : defaultValue;
    }
}