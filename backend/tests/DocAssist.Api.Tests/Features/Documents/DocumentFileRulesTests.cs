using System.Text;
using DocAssist.Api.Features.Documents;

namespace DocAssist.Api.Tests.Features.Documents;

// Tests unitarios de las reglas de los ficheros: sin HTTP, sin disco y sin base de datos.
public class DocumentFileRulesTests
{
    private const long MaxBytes = 1024;

    private static readonly byte[] PdfHeader = Encoding.ASCII.GetBytes("%PDF-1.7\n");
    private static readonly byte[] MarkdownHeader = Encoding.UTF8.GetBytes("# Garantía\n\nDos años.");

    [Theory]
    [InlineData("manual.pdf")]
    [InlineData("MANUAL.PDF")]
    public void ValidPdf_HasNoError(string fileName)
    {
        Assert.Null(DocumentFileRules.Validate(fileName, 100, PdfHeader, MaxBytes));
    }

    [Fact]
    public void ValidMarkdown_HasNoError()
    {
        Assert.Null(DocumentFileRules.Validate("garantia.md", 100, MarkdownHeader, MaxBytes));
    }

    [Theory]
    [InlineData("notas.txt")]
    [InlineData("manual.docx")]
    [InlineData("virus.exe")]
    [InlineData("sin-extension")]
    public void OtherExtensions_AreRejected(string fileName)
    {
        var error = DocumentFileRules.Validate(fileName, 100, MarkdownHeader, MaxBytes);

        Assert.Equal("Only .md and .pdf files are allowed.", error);
    }

    [Fact]
    public void EmptyFile_IsRejected()
    {
        Assert.Equal("The file is empty.", DocumentFileRules.Validate("vacio.md", 0, [], MaxBytes));
    }

    [Fact]
    public void FileOverTheLimit_IsRejected()
    {
        var error = DocumentFileRules.Validate("grande.pdf", MaxBytes + 1, PdfHeader, MaxBytes);

        Assert.StartsWith("The file cannot be larger than", error);
    }

    [Fact]
    public void FileExactlyAtTheLimit_IsAccepted()
    {
        Assert.Null(DocumentFileRules.Validate("justo.pdf", MaxBytes, PdfHeader, MaxBytes));
    }

    [Fact]
    public void PdfWithoutPdfSignature_IsRejected()
    {
        // Un ejecutable de Windows (empieza por "MZ") renombrado a .pdf.
        byte[] executableHeader = [0x4D, 0x5A, 0x90, 0x00];

        var error = DocumentFileRules.Validate("factura.pdf", 100, executableHeader, MaxBytes);

        Assert.Equal("The file is not a valid PDF.", error);
    }

    [Fact]
    public void MarkdownWithBinaryContent_IsRejected()
    {
        var error = DocumentFileRules.Validate("imagen.md", 100, [0x89, 0x50, 0x4E, 0x47, 0x00], MaxBytes);

        Assert.Equal("The file is not a text file.", error);
    }

    [Fact]
    public void FileNameTooLong_IsRejected()
    {
        var fileName = new string('a', 253) + ".md";

        var error = DocumentFileRules.Validate(fileName, 100, MarkdownHeader, MaxBytes);

        Assert.StartsWith("The file name cannot be longer than", error);
    }
}
