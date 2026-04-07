namespace Chatbot.Infrastructure.Persistence;

internal sealed class LocalPersistentSearchScoreCalculator
{
    public double Calculate(string query, float[]? queryEmbedding, LocalPersistentIndexedChunk result)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(result.Content))
        {
            return queryEmbedding is { Length: > 0 } && result.Embedding is { Length: > 0 }
                ? CosineSimilarity(queryEmbedding, result.Embedding)
                : 0.1;
        }

        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var matches = terms.Count(term => result.Content.Contains(term, StringComparison.OrdinalIgnoreCase));
        var lexicalScore = 0.4 + (matches / (double)Math.Max(terms.Length, 1)) * 0.6;

        if (queryEmbedding is not { Length: > 0 } || result.Embedding is not { Length: > 0 })
        {
            return Math.Round(lexicalScore, 2);
        }

        var vectorScore = CosineSimilarity(queryEmbedding, result.Embedding);
        return Math.Round((lexicalScore * 0.4) + (vectorScore * 0.6), 4);
    }

    private static double CosineSimilarity(float[] left, float[] right)
    {
        var length = Math.Min(left.Length, right.Length);
        if (length == 0)
        {
            return 0;
        }

        double dot = 0;
        double leftNorm = 0;
        double rightNorm = 0;

        for (var index = 0; index < length; index++)
        {
            dot += left[index] * right[index];
            leftNorm += left[index] * left[index];
            rightNorm += right[index] * right[index];
        }

        if (leftNorm <= 0 || rightNorm <= 0)
        {
            return 0;
        }

        return (dot / (Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm)) + 1d) / 2d;
    }
}