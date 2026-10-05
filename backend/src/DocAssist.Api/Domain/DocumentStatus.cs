namespace DocAssist.Api.Domain;

// En qué punto de la ingesta está un documento.
public enum DocumentStatus
{
    // Subido y en la cola, esperando a que lo procese el worker.
    Pending,

    // Extrayendo el texto, troceándolo y generando los embeddings.
    Processing,

    // Sus fragmentos ya están en la base de datos y se pueden buscar.
    Ready,

    // La ingesta falló. El motivo está en Document.StatusMessage.
    Failed
}
