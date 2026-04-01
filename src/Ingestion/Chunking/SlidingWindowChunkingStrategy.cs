using Chatbot.Application.Abstractions;

namespace Chatbot.Ingestion.Chunking;

public sealed class SlidingWindowChunkingStrategy : IChunkingStrategy
{
    public List<DocumentChunkIndexDto> Chunk(IngestDocumentCommand command, string text)
    {
        const int chunkSize = 500;
        const int overlap = 80;
        var chunks = new List<DocumentChunkIndexDto>();
        var sourceText = string.IsNullOrWhiteSpace(text) ? command.FileName : text;

        for (var offset = 0; offset < sourceText.Length; offset += Math.Max(1, chunkSize - overlap))
        {
            var length = Math.Min(chunkSize, sourceText.Length - offset);
            var content = sourceText.Substring(offset, length).Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            chunks.Add(new DocumentChunkIndexDto
            {
                ChunkId = $"{command.DocumentId:N}-chunk-{chunks.Count + 1:D4}",
                DocumentId = command.DocumentId,
                Content = content,
                PageNumber = 1,
                Metadata = BuildMetadata(command)
            });

            if (offset + length >= sourceText.Length)
            {
                break;
            }
        }

        if (chunks.Count == 0)
        {
            chunks.Add(new DocumentChunkIndexDto
            {
                ChunkId = $"{command.DocumentId:N}-chunk-0001",
                DocumentId = command.DocumentId,
                Content = sourceText,
                PageNumber = 1,
                Metadata = BuildMetadata(command)
            });
        }

        return chunks;
    }

    private static Dictionary<string, string> BuildMetadata(IngestDocumentCommand command)
    {
        return new Dictionary<string, string>
        {
            ["title"] = string.IsNullOrWhiteSpace(command.DocumentTitle) ? command.FileName : command.DocumentTitle,
            ["contentType"] = command.ContentType,
            ["tenantId"] = command.TenantId.ToString(),
            ["category"] = command.Category ?? string.Empty,
            ["tags"] = string.Join(',', command.Tags),
            ["categories"] = string.Join(',', command.Categories)
        };
    }
}
