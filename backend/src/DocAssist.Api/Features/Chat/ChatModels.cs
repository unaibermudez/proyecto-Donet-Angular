using System.ComponentModel.DataAnnotations;

namespace DocAssist.Api.Features.Chat;

// Lo que el cliente envía a /api/chat y /api/chat/stream. Cada pregunta es
// independiente: no hay memoria de la conversación (ver la documentación del paso 10).
public sealed record AskRequest
{
    [Required, MinLength(3), MaxLength(1000)]
    public string Question { get; init; } = "";

    // Opcional: responder solo con los documentos de un producto.
    public int? ProductId { get; init; }
}

// Respuesta completa de POST /api/chat.
public sealed record AskResponse(
    string Answer,
    List<Citation> Citations,
    long ElapsedMilliseconds);

// Un fragmento que se le pasó al modelo como contexto. Number es el [n] con el que el
// modelo lo cita en la respuesta.
public sealed record Citation(
    int Number,
    int DocumentId,
    string FileName,
    string? ProductName,
    int ChunkIndex,
    string Content,
    double Similarity);

// Eventos del streaming (POST /api/chat/stream), en este orden:
// sources (una vez) → delta (muchos) → done (una vez). Si algo falla: error.
public abstract record ChatStreamEvent(string Type);

public sealed record SourcesEvent(List<Citation> Citations) : ChatStreamEvent("sources");

public sealed record DeltaEvent(string Text) : ChatStreamEvent("delta");

public sealed record DoneEvent(long ElapsedMilliseconds) : ChatStreamEvent("done");

public sealed record ErrorEvent(string Message) : ChatStreamEvent("error");
