using System.Text;
using System.Text.RegularExpressions;

namespace Chatbot.Application.Services;

public sealed class ChatStreamingSegmenter : IChatStreamingSegmenter
{
    private static readonly Regex SentenceBoundaryRegex = new(@"(?<=[\.!\?])\s+", RegexOptions.Compiled);

    public IEnumerable<string> Segment(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        var segments = SentenceBoundaryRegex.Split(text.Trim())
            .Where(segment => !string.IsNullOrWhiteSpace(segment));

        foreach (var segment in segments)
        {
            if (segment.Length <= 96)
            {
                yield return segment.EndsWith(' ') ? segment : $"{segment} ";
                continue;
            }

            var words = segment.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var builder = new StringBuilder();
            foreach (var word in words)
            {
                if (builder.Length > 0 && builder.Length + word.Length + 1 > 72)
                {
                    yield return builder.ToString();
                    builder.Clear();
                }

                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(word);
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
                yield return builder.ToString();
            }
        }
    }
}