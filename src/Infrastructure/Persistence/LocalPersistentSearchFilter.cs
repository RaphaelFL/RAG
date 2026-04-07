using Chatbot.Application.Abstractions;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class LocalPersistentSearchFilter
{
    public bool Matches(LocalPersistentIndexedChunk result, FileSearchFilterDto? filters)
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

        if (filters.Tags is { Count: > 0 })
        {
            var tags = result.Metadata.TryGetValue("tags", out var tagString)
                ? tagString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : Array.Empty<string>();

            if (!filters.Tags.Any(tag => tags.Contains(tag, StringComparer.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        if (filters.Categories is { Count: > 0 })
        {
            var categories = result.Metadata.TryGetValue("categories", out var categoriesString)
                ? categoriesString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : Array.Empty<string>();

            if (!filters.Categories.Any(category => categories.Contains(category, StringComparer.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        if (filters.ContentTypes is { Count: > 0 })
        {
            var contentType = result.Metadata.TryGetValue("contentType", out var contentTypeValue)
                ? contentTypeValue
                : string.Empty;

            if (!filters.ContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (filters.Sources is { Count: > 0 })
        {
            var source = result.Metadata.TryGetValue("source", out var sourceValue)
                ? sourceValue
                : string.Empty;

            if (!filters.Sources.Contains(source, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}