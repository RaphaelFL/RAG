namespace Chatbot.Ingestion.Parsers;

internal sealed class XlsxSharedStringsLoader
{
    private readonly OpenXmlArchiveXmlLoader _xmlLoader;

    public XlsxSharedStringsLoader(OpenXmlArchiveXmlLoader xmlLoader)
    {
        _xmlLoader = xmlLoader;
    }

    public List<string> Load()
    {
        var document = _xmlLoader.LoadXml("xl/sharedStrings.xml");
        if (document is null)
        {
            return new List<string>();
        }

        return document.Descendants()
            .Where(element => element.Name.LocalName == "si")
            .Select(item => string.Concat(item.Descendants().Where(element => element.Name.LocalName == "t").Select(element => element.Value)))
            .ToList();
    }
}