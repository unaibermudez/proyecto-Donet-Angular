using DocAssist.Api.Data;
using DocAssist.Api.Features.Documents.Ingestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace DocAssist.Api.Features.Search;

// Búsqueda semántica: pregunta → embedding → los fragmentos con el vector más cercano.
// Es la "R" (retrieval) del RAG. La usará también el chat (paso 10) y el agente (paso 11).
public sealed class SemanticSearchService(
    AppDbContext db,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    IOptions<OllamaOptions> ollamaOptions)
{
    public const int DefaultTopK = 5;

    public async Task<List<SearchResult>> SearchAsync(
        string question, int topK, int? productId, CancellationToken cancellationToken)
    {
        // La pregunta se convierte en vector con el mismo modelo que los documentos.
        // nomic-embed-text espera el prefijo "search_query: " en las preguntas.
        var embedding = await embeddingGenerator.GenerateVectorAsync(
            ollamaOptions.Value.EmbeddingQueryPrefix + question, cancellationToken: cancellationToken);
        var questionVector = new Vector(embedding);

        var query = db.DocumentChunks.AsNoTracking();
        if (productId is not null)
        {
            query = query.Where(c => c.Document.ProductId == productId);
        }

        // CosineDistance se traduce al operador <=> de pgvector:
        //   ORDER BY embedding <=> @pregunta LIMIT @topK
        // Ordenar por distancia y limitar es justo lo que acelera el índice HNSW.
        var rows = await query
            .OrderBy(c => c.Embedding.CosineDistance(questionVector))
            .Take(topK)
            .Select(c => new
            {
                c.Id,
                c.DocumentId,
                c.Document.FileName,
                c.Document.ProductId,
                ProductName = c.Document.Product != null ? c.Document.Product.Name : null,
                c.Index,
                c.Content,
                Distance = c.Embedding.CosineDistance(questionVector)
            })
            .ToListAsync(cancellationToken);

        // Distancia coseno = 1 - similitud. Para una persona se entiende mejor la similitud.
        return rows
            .Select(r => new SearchResult(
                r.Id, r.DocumentId, r.FileName, r.ProductId, r.ProductName, r.Index, r.Content,
                Math.Round(1 - r.Distance, 4)))
            .ToList();
    }
}
