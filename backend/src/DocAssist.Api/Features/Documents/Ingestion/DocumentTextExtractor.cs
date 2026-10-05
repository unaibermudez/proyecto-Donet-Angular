using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace DocAssist.Api.Features.Documents.Ingestion;

// Saca el texto plano de un fichero, según su tipo.
public static class DocumentTextExtractor
{
    public static string Extract(Stream content, string contentType) => contentType switch
    {
        "text/markdown" => ReadText(content),
        "application/pdf" => ReadPdf(content),
        _ => throw new NotSupportedException($"Cannot extract text from '{contentType}' files.")
    };

    // El Markdown se deja tal cual: los modelos de embeddings entienden bien los #, las
    // tablas y las listas, y el fragmento se lee mejor si luego se enseña como cita.
    private static string ReadText(Stream content)
    {
        using var reader = new StreamReader(content, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    // Cada página se separa con una línea en blanco, así el troceador la trata como un
    // párrafo aparte. ContentOrderTextExtractor respeta el orden de lectura y los saltos
    // de línea mejor que page.Text, que junta todas las letras de la página.
    // Un PDF escaneado (solo imágenes) no tiene texto: haría falta OCR.
    private static string ReadPdf(Stream content)
    {
        using var pdf = PdfDocument.Open(content);
        return string.Join("\n\n", pdf.GetPages().Select(page => ContentOrderTextExtractor.GetText(page)));
    }
}
