using System.Text;
using Chatbot.Application.Abstractions;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class RedisStackFilterQueryBuilder
{
    public string Build(FileSearchFilterDto? filters)
    {
        var clauses = new List<string>();

        if (filters?.TenantId is Guid tenantId && tenantId != Guid.Empty)
        {
            clauses.Add($"@tenantId:{{{EscapeTag(tenantId.ToString())}}}");
        }

        if (filters?.DocumentIds is { Count: > 0 })
        {
            clauses.Add(BuildTagClause("documentId", filters.DocumentIds.Select(id => id.ToString())));
        }

        if (filters?.Tags is { Count: > 0 })
        {
            clauses.Add(BuildTagClause("tags", filters.Tags));
        }

        if (filters?.Categories is { Count: > 0 })
        {
            clauses.Add(BuildTagClause("categories", filters.Categories));
        }

        if (filters?.ContentTypes is { Count: > 0 })
        {
            clauses.Add(BuildTagClause("contentType", filters.ContentTypes));
        }

        if (filters?.Sources is { Count: > 0 })
        {
            clauses.Add(BuildTagClause("sourceType", filters.Sources));
        }

        return clauses.Count == 0 ? "*" : string.Join(' ', clauses);
    }

    public string EscapeTextQuery(string query)
    {
        var tokens = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', tokens.Select(token => token.Replace("-", "\\-", StringComparison.Ordinal)));
    }

    private static string BuildTagClause(string fieldName, IEnumerable<string> values)
    {
        var normalized = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(EscapeTag)
            .ToArray();

        return normalized.Length == 0
            ? string.Empty
            : $"@{fieldName}:{{{string.Join('|', normalized)}}}";
    }

    private static string EscapeTag(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (character is '\\' or '-' or '|' or '{' or '}' or '[' or ']' or '(' or ')' or '"' or ':' or ';' or ',' or '.' or '<' or '>' or '~' or '!' or '@' or '#')
            {
                builder.Append('\\');
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}