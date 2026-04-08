using Chatbot.Application.Abstractions;

namespace Chatbot.Ingestion.Parsers;

internal sealed class PptxSlideExtractionBuilder
{
    public PageExtractionDto BuildPage(int slideNumber, string title, string slideText, string notesText)
    {
        return new PageExtractionDto
        {
            PageNumber = slideNumber,
            SlideNumber = slideNumber,
            SectionTitle = title,
            Text = slideText,
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["slideNumber"] = slideNumber.ToString(),
                ["title"] = title,
                ["hasNotes"] = (!string.IsNullOrWhiteSpace(notesText)).ToString()
            }
        };
    }

    public object BuildStructuredSlide(int slideNumber, string title, IReadOnlyList<string> bullets, string notesText)
    {
        return new
        {
            slideNumber,
            title,
            bullets,
            notes = notesText
        };
    }
}