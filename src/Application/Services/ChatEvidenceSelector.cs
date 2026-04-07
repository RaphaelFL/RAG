using System.Text;

namespace Chatbot.Application.Services;

public sealed class ChatEvidenceSelector : IChatEvidenceSelector
{
    private static readonly HashSet<string> EvidenceStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "o", "as", "os", "de", "do", "da", "dos", "das", "e", "em", "no", "na", "nos", "nas",
        "para", "por", "com", "sem", "sobre", "qual", "quais", "como", "que", "uma", "um", "ao", "aos",
        "se", "ou", "the", "and", "for", "with", "from", "this", "that"
    };

    public IReadOnlyList<RetrievedChunkDto> Select(string message, IReadOnlyCollection<RetrievedChunkDto> chunks, int maxContextChunks)
    {
        if (chunks.Count == 0)
        {
            return Array.Empty<RetrievedChunkDto>();
        }

        var queryTerms = TokenizeEvidenceTerms(message);
        return chunks
            .Where(IsReadableChunk)
            .Select(chunk => new
            {
                Chunk = chunk,
                Score = ScoreEvidenceChunk(chunk, queryTerms)
            })
            .Where(item => item.Score >= (queryTerms.Count == 0 ? 0.15 : 0.35))
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Chunk.Score)
            .Take(maxContextChunks)
            .Select(item => item.Chunk)
            .ToList();
    }

    private static double ScoreEvidenceChunk(RetrievedChunkDto chunk, HashSet<string> queryTerms)
    {
        if (queryTerms.Count == 0)
        {
            return Math.Min(1, chunk.Score * 0.2);
        }

        var contentTerms = TokenizeEvidenceTerms($"{chunk.DocumentTitle} {chunk.Section} {chunk.Content}");
        var overlap = contentTerms.Count(queryTerms.Contains);
        var coverage = overlap / (double)Math.Max(1, Math.Min(queryTerms.Count, 6));
        var score = coverage + Math.Min(1, chunk.Score) * 0.2;

        if (chunk.DocumentTitle.ContainsAny(queryTerms))
        {
            score += 0.15;
        }

        if (!string.IsNullOrWhiteSpace(chunk.Section) && chunk.Section.ContainsAny(queryTerms))
        {
            score += 0.1;
        }

        return Math.Round(score, 4);
    }

    private static HashSet<string> TokenizeEvidenceTerms(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.ToLowerInvariant())
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }

        return builder.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(term => term.Length >= 3)
            .Where(term => !EvidenceStopWords.Contains(term))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsReadableChunk(RetrievedChunkDto chunk)
    {
        if (string.IsNullOrWhiteSpace(chunk.Content) || PdfTextExtraction.LooksLikeArtifactText(chunk.Content))
        {
            return false;
        }

        var text = chunk.Content.Trim();
        var lettersOrDigits = text.Count(char.IsLetterOrDigit);
        var controlCharacters = text.Count(character => char.IsControl(character) && !char.IsWhiteSpace(character));
        var punctuationNoise = text.Count(character => !char.IsLetterOrDigit(character) && !char.IsWhiteSpace(character) && ".,;:!?%()[]{}-_/\\\"'".IndexOf(character) < 0);

        return lettersOrDigits >= 24
            && controlCharacters == 0
            && punctuationNoise / (double)Math.Max(1, text.Length) < 0.12;
    }
}