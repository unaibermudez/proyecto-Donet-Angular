using DocAssist.Api.Domain;

namespace DocAssist.Api.Features.Documents;

// Lo que la API devuelve de un documento. No incluye dónde está guardado en disco.
public sealed record DocumentResponse(
    int Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTime UploadedAt,
    int? ProductId,
    string? ProductName)
{
    // document.Product tiene que estar cargado (Include) si el documento tiene producto.
    public static DocumentResponse FromEntity(Document document) => new(
        document.Id,
        document.FileName,
        document.ContentType,
        document.SizeBytes,
        document.UploadedAt,
        document.ProductId,
        document.Product?.Name);
}
