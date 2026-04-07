using System.Security.Cryptography;
using System.Text;

namespace Chatbot.Application.Services;

public sealed class RetrievalCacheKeyFactory : IRetrievalCacheKeyFactory
{
    public string Build(string query, int requestedTopK, int candidateCount, bool semanticRankingEnabled, FileSearchFilterDto filters, IRequestContextAccessor requestContextAccessor, IDocumentCatalog documentCatalog)
    {
        var tenantId = requestContextAccessor.TenantId?.ToString() ?? string.Empty;
        var userId = requestContextAccessor.UserId ?? string.Empty;
        var userRole = requestContextAccessor.UserRole ?? string.Empty;
        var documentIds = filters.DocumentIds is { Count: > 0 }
            ? string.Join(',', filters.DocumentIds.OrderBy(id => id))
            : string.Empty;
        var tags = filters.Tags is { Count: > 0 }
            ? string.Join(',', filters.Tags.OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase))
            : string.Empty;
        var categories = filters.Categories is { Count: > 0 }
            ? string.Join(',', filters.Categories.OrderBy(category => category, StringComparer.OrdinalIgnoreCase))
            : string.Empty;
        var contentTypes = filters.ContentTypes is { Count: > 0 }
            ? string.Join(',', filters.ContentTypes.OrderBy(contentType => contentType, StringComparer.OrdinalIgnoreCase))
            : string.Empty;
        var sources = filters.Sources is { Count: > 0 }
            ? string.Join(',', filters.Sources.OrderBy(source => source, StringComparer.OrdinalIgnoreCase))
            : string.Empty;
        var coherencyStamp = ResolveCacheCoherencyStamp(filters, documentCatalog);

        return $"retrieval:{ComputeHash(string.Join("||", new[]
        {
            query.Trim(),
            requestedTopK.ToString(),
            candidateCount.ToString(),
            semanticRankingEnabled.ToString(),
            tenantId,
            userId,
            userRole,
            documentIds,
            tags,
            categories,
            contentTypes,
            sources,
            coherencyStamp
        }))}";
    }

    private static string ResolveCacheCoherencyStamp(FileSearchFilterDto filters, IDocumentCatalog documentCatalog)
    {
        var scopedDocuments = documentCatalog.Query(new FileSearchFilterDto
        {
            TenantId = filters.TenantId,
            DocumentIds = filters.DocumentIds,
            Tags = filters.Tags,
            Categories = filters.Categories,
            ContentTypes = filters.ContentTypes,
            Sources = filters.Sources
        });

        if (scopedDocuments.Count == 0)
        {
            return "empty-scope";
        }

        return ComputeHash(string.Join('|', scopedDocuments
            .OrderBy(document => document.DocumentId)
            .Select(document => $"{document.DocumentId:N}:{document.Version}:{document.UpdatedAtUtc.Ticks}")));
    }

    private static string ComputeHash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}