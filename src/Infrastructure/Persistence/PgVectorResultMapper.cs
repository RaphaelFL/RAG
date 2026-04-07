using System.Data;
using System.Globalization;
using System.Text.Json;
using Chatbot.Application.Abstractions;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class PgVectorResultMapper
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public DocumentChunkIndexDto MapChunk(IDataRecord reader)
    {
        var metadataJson = reader.GetString(reader.GetOrdinal("metadata"));
        var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(metadataJson, SerializerOptions)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return new DocumentChunkIndexDto
        {
            ChunkId = reader.GetString(reader.GetOrdinal("chunk_id")),
            DocumentId = reader.GetGuid(reader.GetOrdinal("document_id")),
            Content = reader.GetString(reader.GetOrdinal("content")),
            Embedding = ParseVectorText(reader.GetString(reader.GetOrdinal("embedding_text"))),
            PageNumber = reader.GetInt32(reader.GetOrdinal("page_number")),
            Section = reader.GetString(reader.GetOrdinal("section_title")),
            Metadata = metadata
        };
    }

    public SearchResultDto MapSearchResult(IDataRecord reader)
    {
        var metadataJson = reader.GetString(reader.GetOrdinal("metadata"));
        var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(metadataJson, SerializerOptions)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return new SearchResultDto
        {
            ChunkId = reader.GetString(reader.GetOrdinal("chunk_id")),
            DocumentId = reader.GetGuid(reader.GetOrdinal("document_id")),
            Content = reader.GetString(reader.GetOrdinal("content")),
            Score = reader.GetDouble(reader.GetOrdinal("score")),
            Metadata = metadata
        };
    }

    private static float[]? ParseVectorText(string rawVector)
    {
        if (string.IsNullOrWhiteSpace(rawVector))
        {
            return null;
        }

        var normalized = rawVector.Trim();
        if (normalized.Length >= 2 && normalized[0] == '[' && normalized[^1] == ']')
        {
            normalized = normalized[1..^1];
        }

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Array.Empty<float>();
        }

        return normalized
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => float.Parse(value, CultureInfo.InvariantCulture))
            .ToArray();
    }
}