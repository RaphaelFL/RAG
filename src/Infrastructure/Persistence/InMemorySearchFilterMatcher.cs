using Chatbot.Application.Abstractions;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class InMemorySearchFilterMatcher
{
    public bool Matches(InMemoryIndexedChunk result, FileSearchFilterDto? filters)
    {
        if (filters is null)
        {
            return true;
        }

        if (filters.DocumentIds is { Count: > 0 } && !filters.DocumentIds.Contains(result.DocumentId))
        {
            return false;
        }

        if (filters.TenantId.HasValue && result.Metadata.TryGetValue("tenantId", out var tenantId) && tenantId != filters.TenantId.Value.ToString())
        {
            return false;
        }

        if (filters.Tags is { Count: > 0 } && !HasAnyMatch(result, "tags", filters.Tags))
        {
            return false;
        }

        if (filters.Categories is { Count: > 0 } && !HasAnyMatch(result, "categories", filters.Categories))
        {
            return false;
        }

        if (filters.ContentTypes is { Count: > 0 } && !HasExactMatch(result, "contentType", filters.ContentTypes))
        {
            return false;
        }

        if (filters.Sources is { Count: > 0 } && !HasExactMatch(result, "source", filters.Sources))
        {
            return false;
        }

        return true;
    }

    private static bool HasAnyMatch(InMemoryIndexedChunk result, string metadataKey, IReadOnlyCollection<string> expectedValues)
    {
        var values = result.Metadata.TryGetValue(metadataKey, out var rawValue)
            ? rawValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : Array.Empty<string>();

        return expectedValues.Any(value => values.Contains(value, StringComparer.OrdinalIgnoreCase));
    }

    private static bool HasExactMatch(InMemoryIndexedChunk result, string metadataKey, IReadOnlyCollection<string> expectedValues)
    {
        var value = result.Metadata.TryGetValue(metadataKey, out var rawValue)
            ? rawValue
            : string.Empty;

        return expectedValues.Contains(value, StringComparer.OrdinalIgnoreCase);
    }
}