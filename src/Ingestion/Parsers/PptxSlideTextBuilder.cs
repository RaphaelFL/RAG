using System.Text;

namespace Chatbot.Ingestion.Parsers;

internal sealed class PptxSlideTextBuilder
{
    public string Build(int slideNumber, string title, IReadOnlyList<string> paragraphs, string notesText)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# Slide {slideNumber}: {title}");

        foreach (var paragraph in paragraphs)
        {
            builder.AppendLine(paragraph);
        }

        if (!string.IsNullOrWhiteSpace(notesText))
        {
            builder.AppendLine();
            builder.AppendLine("Speaker notes:");
            builder.AppendLine(notesText);
        }

        return builder.ToString().Trim();
    }
}