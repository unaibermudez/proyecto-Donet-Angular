using System.Text;
using DocAssist.Api.Features.Documents.Ingestion;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace DocAssist.Api.Tests.Features.Documents.Ingestion;

public class DocumentTextExtractorTests
{
    [Fact]
    public void Markdown_IsReturnedAsIs()
    {
        const string markdown = "# Envíos\n\n| Modalidad | Precio |\n|---|---|\n| Urgente | 9,99 € |";

        var text = DocumentTextExtractor.Extract(new MemoryStream(Encoding.UTF8.GetBytes(markdown)), "text/markdown");

        Assert.Equal(markdown, text);
    }

    [Fact]
    public void Pdf_ReturnsTheTextOfEveryPage()
    {
        // Se crea un PDF de dos páginas con el propio PdfPig.
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        builder.AddPage(PageSize.A4).AddText("Primera pagina del manual", 12, new PdfPoint(50, 700), font);
        builder.AddPage(PageSize.A4).AddText("Segunda pagina del manual", 12, new PdfPoint(50, 700), font);
        var pdf = builder.Build();

        var text = DocumentTextExtractor.Extract(new MemoryStream(pdf), "application/pdf");

        Assert.Contains("Primera pagina del manual", text);
        Assert.Contains("Segunda pagina del manual", text);
        // Las páginas quedan separadas por una línea en blanco: el troceador las ve como párrafos.
        Assert.Contains("\n\n", text);
    }

    [Fact]
    public void OtherTypes_AreNotSupported()
    {
        Assert.Throws<NotSupportedException>(() =>
            DocumentTextExtractor.Extract(new MemoryStream(), "image/png"));
    }
}
