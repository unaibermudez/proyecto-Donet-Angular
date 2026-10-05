using System.ComponentModel.DataAnnotations;

namespace DocAssist.Api.Features.Documents.Ingestion;

// Sección "Ingestion" de appsettings.json: cómo se trocean los documentos.
public sealed class IngestionOptions
{
    public const string SectionName = "Ingestion";

    // Tamaño máximo de cada fragmento, en caracteres. Unos 1.000 caracteres son
    // 250-300 tokens: suficiente contexto para responder y bastante preciso al buscar.
    [Range(200, 4000)]
    public int ChunkSize { get; set; } = 1000;

    // Caracteres que se repiten entre un fragmento y el siguiente. Como mucho la mitad
    // de ChunkSize (lo comprueba TextChunker).
    [Range(0, 2000)]
    public int ChunkOverlap { get; set; } = 200;
}

// Sección "Ollama": el servidor de modelos local y sus modelos (embeddings y chat).
public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    [Required]
    public Uri Endpoint { get; set; } = null!;

    [Required]
    public string EmbeddingModel { get; set; } = "";

    // nomic-embed-text da mejores resultados si al texto que se guarda se le antepone
    // "search_document: ", y a las preguntas "search_query: ".
    // Depende del modelo: con otro puede ir vacío.
    public string EmbeddingDocumentPrefix { get; set; } = "";

    public string EmbeddingQueryPrefix { get; set; } = "";

    // Modelo que redacta las respuestas del chat.
    [Required]
    public string ChatModel { get; set; } = "";

    // En CPU, la primera respuesta incluye cargar el modelo en memoria (~50 s) y luego
    // generar a ~6 tokens/s. Los 100 s por defecto de HttpClient se quedan cortos.
    [Range(10, 1800)]
    public int RequestTimeoutSeconds { get; set; } = 300;
}
