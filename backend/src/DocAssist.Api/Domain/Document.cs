namespace DocAssist.Api.Domain;

// Un fichero subido a la tienda: el manual de un producto o un documento general
// (garantía, devoluciones, envíos). El contenido está en disco; aquí solo sus datos.
public class Document
{
    public int Id { get; set; }

    // Nombre original del fichero. Solo se usa para mostrarlo y al descargarlo.
    public required string FileName { get; set; }

    public required string ContentType { get; set; }

    public long SizeBytes { get; set; }

    // Nombre con el que se guardó en disco (un GUID). Nunca se usa el del usuario,
    // que podría repetirse o traer rutas como "../../".
    public required string StoredFileName { get; set; }

    // Siempre en UTC.
    public DateTime UploadedAt { get; set; }

    // Opcional: los documentos generales no pertenecen a ningún producto.
    public int? ProductId { get; set; }

    public Product? Product { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;

    // Por qué falló la ingesta. Solo tiene valor si Status es Failed.
    public string? StatusMessage { get; set; }

    public List<DocumentChunk> Chunks { get; } = [];
}
