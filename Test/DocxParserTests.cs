using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentParser.Parsers;
using Xunit;

namespace Test;

public class DocxParserTests
{
    [Fact]
    public void Parse_DocxWithParagraphText_ReturnsText()
    {
        using var stream = CreateDocx(new Paragraph(new Run(new Text("Plain DOCX text"))));

        var result = new DocxParser().Parse(stream, "plain.docx");

        var page = Assert.Single(result.Pages);
        Assert.False(page.IsEmpty);
        Assert.Contains("Plain DOCX text", page.Text);
    }

    [Fact]
    public void Parse_DocxWithContentControlText_ReturnsNestedText()
    {
        var contentControl = new SdtBlock(
            new SdtContentBlock(
                new Paragraph(new Run(new Text("Nested content control text")))));
        using var stream = CreateDocx(contentControl);

        var result = new DocxParser().Parse(stream, "content-control.docx");

        var page = Assert.Single(result.Pages);
        Assert.False(page.IsEmpty);
        Assert.Contains("Nested content control text", page.Text);
    }

    private static MemoryStream CreateDocx(params OpenXmlElement[] bodyElements)
    {
        var stream = new MemoryStream();

        using (var document = WordprocessingDocument.Create(
            stream,
            DocumentFormat.OpenXml.WordprocessingDocumentType.Document,
            autoSave: true))
        {
            MainDocumentPart mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body(bodyElements));
        }

        stream.Position = 0;
        return stream;
    }
}
