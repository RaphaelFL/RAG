using System.IO.Compression;
using System.Xml.Linq;

namespace Chatbot.Ingestion.Parsers;

internal sealed class OpenXmlArchiveXmlLoader
{
    private readonly ZipArchive _archive;

    public OpenXmlArchiveXmlLoader(ZipArchive archive)
    {
        _archive = archive;
    }

    public XDocument? LoadXml(string path)
    {
        var entry = _archive.GetEntry(path.Replace('\u005C', '/'));
        if (entry is null)
        {
            return null;
        }

        using var stream = entry.Open();
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    }
}