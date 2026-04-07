using System.Text.Json;
using Chatbot.Application.Abstractions;

namespace Chatbot.Infrastructure.Configuration;

public sealed class DocumentAuthorizationService : IDocumentAuthorizationService
{
    public bool CanAccess(DocumentCatalogEntry document, Guid? tenantId, string? userId, string? userRole)
    {
        if (!tenantId.HasValue)
        {
            return false;
        }

        var policy = Parse(document.AccessPolicy);
        var sameTenant = tenantId.Value == document.TenantId;
        var normalizedRole = userRole?.Trim();
        var normalizedUserId = userId?.Trim();

        if (!sameTenant)
        {
            return string.Equals(normalizedRole, "PlatformAdmin", StringComparison.OrdinalIgnoreCase) &&
                policy.AllowPlatformAdminCrossTenant;
        }

        if (policy.AllowedUserIds.Count > 0)
        {
            return !string.IsNullOrWhiteSpace(normalizedUserId) &&
                policy.AllowedUserIds.Contains(normalizedUserId, StringComparer.OrdinalIgnoreCase);
        }

        if (policy.AllowedRoles.Count > 0)
        {
            return !string.IsNullOrWhiteSpace(normalizedRole) &&
                policy.AllowedRoles.Contains(normalizedRole, StringComparer.OrdinalIgnoreCase);
        }

        return true;
    }

    private static ParsedAccessPolicy Parse(string? rawPolicy)
    {
        if (string.IsNullOrWhiteSpace(rawPolicy))
        {
            return ParsedAccessPolicy.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(rawPolicy);
            var root = document.RootElement;
            return new ParsedAccessPolicy
            {
                AllowPlatformAdminCrossTenant = root.TryGetProperty("allowPlatformAdminCrossTenant", out var crossTenantElement) &&
                    crossTenantElement.ValueKind is JsonValueKind.True,
                AllowedRoles = ReadArray(root, "allowedRoles"),
                AllowedUserIds = ReadArray(root, "allowedUserIds")
            };
        }
        catch (JsonException)
        {
            var roles = rawPolicy
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();

            return new ParsedAccessPolicy
            {
                AllowedRoles = roles
            };
        }
    }

    private static List<string> ReadArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return new List<string>();
        }

        return property.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToList();
    }
}