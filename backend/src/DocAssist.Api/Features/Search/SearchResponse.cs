namespace DocAssist.Api.Features.Search;

// Respuesta de POST /api/search: los fragmentos más parecidos a la pregunta, del más
// parecido al menos parecido.
public sealed record SearchResponse(
    string Question,
    List<SearchResult> Results,
    long ElapsedMilliseconds);

// Un fragmento encontrado. Similarity es la similitud coseno: 1 = mismo significado,
// 0 = nada que ver. Con nomic-embed-text, los resultados buenos suelen pasar de 0,65.
public sealed record SearchResult(
    long ChunkId,
    int DocumentId,
    string FileName,
    int? ProductId,
    string? ProductName,
    int ChunkIndex,
    string Content,
    double Similarity);
