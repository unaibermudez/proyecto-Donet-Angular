using Pgvector;

namespace DocAssist.Api.Domain;

// Un fragmento del texto de un documento, con su embedding. Es la unidad que se
// busca en el RAG: a la pregunta se le devuelven fragmentos, no documentos enteros.
public class DocumentChunk
{
    // Tamaño de los vectores de nomic-embed-text. Si se cambia de modelo de
    // embeddings, hay que cambiar la columna y volver a procesar todos los documentos.
    public const int EmbeddingDimensions = 768;

    public long Id { get; set; }

    public int DocumentId { get; set; }

    public Document Document { get; set; } = null!;

    // Posición del fragmento dentro del documento (0, 1, 2...).
    public int Index { get; set; }

    // Texto original del fragmento: es lo que se le pasará al LLM como contexto.
    public required string Content { get; set; }

    public required Vector Embedding { get; set; }
}
