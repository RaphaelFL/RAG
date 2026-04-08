namespace Chatbot.Ingestion.Parsers;

internal sealed class PptxSlideNotesExtractor
{
    private readonly OpenXmlArchiveXmlLoader _xmlLoader;
    private readonly OpenXmlRelationshipTargetResolver _relationshipTargetResolver;

    public PptxSlideNotesExtractor(OpenXmlArchiveXmlLoader xmlLoader, OpenXmlRelationshipTargetResolver relationshipTargetResolver)
    {
        _xmlLoader = xmlLoader;
        _relationshipTargetResolver = relationshipTargetResolver;
    }

    public string TryExtract(string slidePath)
    {
        var slideFileName = Path.GetFileName(slidePath);
        if (string.IsNullOrWhiteSpace(slideFileName))
        {
            return string.Empty;
        }

        var slideRelationshipsPath = $"ppt/slides/_rels/{slideFileName}.rels";
        var slideRelationships = _xmlLoader.LoadXml(slideRelationshipsPath);
        if (slideRelationships is null)
        {
            return string.Empty;
        }

        var notesPath = _relationshipTargetResolver.ResolveTargetByTypeSuffix(slideRelationships, "ppt/slides", "notesSlide");
        if (string.IsNullOrWhiteSpace(notesPath))
        {
            return string.Empty;
        }

        var notes = _xmlLoader.LoadXml(notesPath);
        if (notes is null)
        {
            return string.Empty;
        }

        return string.Join('\n', notes.Descendants()
            .Where(element => element.Name.LocalName == "t")
            .Select(element => NormalizeWhitespace(element.Value))
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string NormalizeWhitespace(string value)
    {
        return string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}