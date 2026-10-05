namespace DocAssist.Api.Features.Documents;

// Dónde se guarda el contenido de los documentos. Con una interfaz, cambiar el disco
// local por un almacenamiento en la nube (Azure Blob, S3) no toca los endpoints.
public interface IDocumentStorage
{
    // Guarda el contenido con un nombre nuevo y único, y devuelve ese nombre.
    Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken);

    // null si el fichero no existe.
    Stream? OpenRead(string storedFileName);

    // No falla si el fichero ya no existe.
    void Delete(string storedFileName);
}
