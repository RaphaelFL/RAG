using System.IO.Compression;
using System.Text;
using Chatbot.Application.Abstractions;
using Chatbot.Ingestion.Parsers;
using FluentAssertions;
using Xunit;

namespace Backend.Unit;

public class SpecializedDocumentParsersTests
{
    [Fact]
    public async Task XlsxParser_ShouldExtractWorksheetTextAndStructure()
    {
        var parser = new XlsxDocumentParser();
        var payload = CreateMinimalXlsx();
        var command = new IngestDocumentCommand
        {
            DocumentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            FileName = "relatorio.xlsx",
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ContentLength = payload.Length,
            Content = new MemoryStream(payload, writable: false)
        };

        var result = await parser.ParseAsync(command, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Text.Should().Contain("Worksheet: Financeiro");
        result.Text.Should().Contain("Conta: Receita");
        result.Pages.Should().ContainSingle();
        result.Pages[0].WorksheetName.Should().Be("Financeiro");
        result.Pages[0].TableId.Should().NotBeNullOrWhiteSpace();
        result.StructuredJson.Should().Contain("worksheetCount");
    }

    [Fact]
    public async Task PptxParser_ShouldExtractSlidesAndNotes()
    {
        var parser = new PptxDocumentParser();
        var payload = CreateMinimalPptx();
        var command = new IngestDocumentCommand
        {
            DocumentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            FileName = "deck.pptx",
            ContentType = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ContentLength = payload.Length,
            Content = new MemoryStream(payload, writable: false)
        };

        var result = await parser.ParseAsync(command, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Text.Should().Contain("Slide 1");
        result.Text.Should().Contain("Resumo Executivo");
        result.Text.Should().Contain("Metricas principais");
        result.Text.Should().Contain("Observacao do apresentador");
        result.Pages.Should().ContainSingle();
        result.Pages[0].SlideNumber.Should().Be(1);
        result.Pages[0].SectionTitle.Should().Be("Resumo Executivo");
    }

    private static byte[] CreateMinimalXlsx()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(archive, "xl/workbook.xml", """
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets>
                    <sheet name="Financeiro" sheetId="1" r:id="rId1" />
                  </sheets>
                </workbook>
                """);
            AddEntry(archive, "xl/_rels/workbook.xml.rels", """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml" />
                </Relationships>
                """);
            AddEntry(archive, "xl/worksheets/sheet1.xml", """
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <sheetData>
                    <row r="1">
                      <c r="A1" t="inlineStr"><is><t>Conta</t></is></c>
                      <c r="B1" t="inlineStr"><is><t>Valor</t></is></c>
                    </row>
                    <row r="2">
                      <c r="A2" t="inlineStr"><is><t>Receita</t></is></c>
                      <c r="B2" t="inlineStr"><is><t>1000</t></is></c>
                    </row>
                  </sheetData>
                </worksheet>
                """);
        }

        return stream.ToArray();
    }

    private static byte[] CreateMinimalPptx()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(archive, "ppt/presentation.xml", """
                <p:presentation xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <p:sldIdLst>
                    <p:sldId id="256" r:id="rId1" />
                  </p:sldIdLst>
                </p:presentation>
                """);
            AddEntry(archive, "ppt/_rels/presentation.xml.rels", """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide" Target="slides/slide1.xml" />
                </Relationships>
                """);
            AddEntry(archive, "ppt/slides/slide1.xml", """
                <p:sld xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
                  <p:cSld>
                    <p:spTree>
                      <p:sp><p:txBody><a:p><a:r><a:t>Resumo Executivo</a:t></a:r></a:p></p:txBody></p:sp>
                      <p:sp><p:txBody><a:p><a:r><a:t>Metricas principais</a:t></a:r></a:p></p:txBody></p:sp>
                    </p:spTree>
                  </p:cSld>
                </p:sld>
                """);
            AddEntry(archive, "ppt/slides/_rels/slide1.xml.rels", """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/notesSlide" Target="../notesSlides/notesSlide1.xml" />
                </Relationships>
                """);
            AddEntry(archive, "ppt/notesSlides/notesSlide1.xml", """
                <p:notes xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
                  <p:cSld><p:spTree><p:sp><p:txBody><a:p><a:r><a:t>Observacao do apresentador</a:t></a:r></a:p></p:txBody></p:sp></p:spTree></p:cSld>
                </p:notes>
                """);
        }

        return stream.ToArray();
    }

    private static void AddEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}