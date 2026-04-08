using System.Xml.Linq;

namespace Chatbot.Ingestion.Parsers;

internal sealed class OpenXmlRelationshipTargetResolver
{
    public Dictionary<string, string> ResolveTargets(XDocument relationshipsDocument, string baseDirectory)
    {
        return relationshipsDocument.Descendants()
            .Where(element => element.Name.LocalName == "Relationship")
            .Select(element => new
            {
                Id = element.Attribute("Id")?.Value,
                Target = element.Attribute("Target")?.Value
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.Target))
            .ToDictionary(
                item => item.Id!,
                item => ResolveZipPath(baseDirectory, item.Target!),
                StringComparer.OrdinalIgnoreCase);
    }

    public string? ResolveTargetByTypeSuffix(XDocument relationshipsDocument, string baseDirectory, string relationshipTypeSuffix)
    {
        var relationship = relationshipsDocument.Descendants()
            .Where(element => element.Name.LocalName == "Relationship")
            .FirstOrDefault(element => string.Equals(
                element.Attribute("Type")?.Value?.Split('/').LastOrDefault(),
                relationshipTypeSuffix,
                StringComparison.OrdinalIgnoreCase));

        var target = relationship?.Attribute("Target")?.Value;
        return string.IsNullOrWhiteSpace(target)
            ? null
            : ResolveZipPath(baseDirectory, target);
    }

    public string? ResolveRelationshipId(XElement element)
    {
        return element.Attributes()
            .FirstOrDefault(attribute => attribute.Name.LocalName == "id" && attribute.Name.NamespaceName.Contains("relationships", StringComparison.OrdinalIgnoreCase))
            ?.Value;
    }

    private static string ResolveZipPath(string baseDirectory, string target)
    {
        var normalizedBase = baseDirectory.Replace('\\', '/').Trim('/');
        var combined = target.StartsWith("/", StringComparison.Ordinal)
            ? target.TrimStart('/')
            : string.Join('/', new[] { normalizedBase, target }.Where(segment => !string.IsNullOrWhiteSpace(segment)));

        var parts = new Stack<string>();
        foreach (var segment in combined.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (parts.Count > 0)
                {
                    parts.Pop();
                }

                continue;
            }

            parts.Push(segment);
        }

        return string.Join('/', parts.Reverse());
    }
}