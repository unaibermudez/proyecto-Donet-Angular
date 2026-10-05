using System.Diagnostics;
using DocAssist.Api.Data;
using DocAssist.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Pgvector;

namespace DocAssist.Api.Features.Documents.Ingestion;

// La ingesta de un documento: texto → fragmentos → embeddings → base de datos.
public sealed class DocumentIngestionService(
    AppDbContext db,
    IDocumentStorage storage,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    IOptions<IngestionOptions> ingestionOptions,
    IOptions<OllamaOptions> ollamaOptions,
    ILogger<DocumentIngestionService> logger)
{
    // Cuántos fragmentos se mandan a Ollama en cada petición.
    private const int EmbeddingBatchSize = 32;

    public async Task IngestAsync(int documentId, CancellationToken cancellationToken)
    {
        var document = await db.Documents
            .Include(d => d.Product)
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);
        if (document is null)
        {
            logger.LogWarning("Document {DocumentId} was deleted before it could be ingested", documentId);
            return;
        }

        document.Status = DocumentStatus.Processing;
        document.StatusMessage = null;
        await db.SaveChangesAsync(cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var chunks = ReadChunks(document);
            var embeddings = await GenerateEmbeddingsAsync(document, chunks, cancellationToken);

            // Borrar los fragmentos anteriores (si se reprocesa) y guardar los nuevos en una
            // sola transacción: o queda todo nuevo, o todo como estaba.
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            await db.DocumentChunks
                .Where(c => c.DocumentId == documentId)
                .ExecuteDeleteAsync(cancellationToken);

            db.DocumentChunks.AddRange(chunks.Select((content, index) => new DocumentChunk
            {
                DocumentId = documentId,
                Index = index,
                Content = content,
                Embedding = new Vector(embeddings[index])
            }));
            document.Status = DocumentStatus.Ready;
            await db.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Document {DocumentId} ingested: {ChunkCount} chunks in {ElapsedMilliseconds} ms",
                documentId, chunks.Count, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Ingestion of document {DocumentId} failed", documentId);

            // El contexto puede tener cambios a medias: se descartan y se actualiza solo el estado.
            db.ChangeTracker.Clear();
            var message = exception.Message.Length > 1000 ? exception.Message[..1000] : exception.Message;
            await db.Documents
                .Where(d => d.Id == documentId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(d => d.Status, DocumentStatus.Failed)
                    .SetProperty(d => d.StatusMessage, message),
                    CancellationToken.None);
        }
    }

    private List<string> ReadChunks(Document document)
    {
        using var content = storage.OpenRead(document.StoredFileName)
            ?? throw new InvalidOperationException("The file is missing from storage.");

        var text = DocumentTextExtractor.Extract(content, document.ContentType);
        var options = ingestionOptions.Value;
        var chunks = TextChunker.Split(text, options.ChunkSize, options.ChunkOverlap);

        return chunks.Count > 0
            ? chunks
            : throw new InvalidOperationException(
                "The document has no text. If it is a scanned PDF, it would need OCR.");
    }

    private async Task<List<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(
        Document document, List<string> chunks, CancellationToken cancellationToken)
    {
        var prefix = ollamaOptions.Value.EmbeddingDocumentPrefix + ContextHeader(document);
        var vectors = new List<ReadOnlyMemory<float>>(chunks.Count);

        foreach (var batch in chunks.Chunk(EmbeddingBatchSize))
        {
            var embeddings = await embeddingGenerator.GenerateAsync(
                batch.Select(chunk => prefix + chunk), cancellationToken: cancellationToken);

            foreach (var embedding in embeddings)
            {
                if (embedding.Vector.Length != DocumentChunk.EmbeddingDimensions)
                {
                    throw new InvalidOperationException(
                        $"The embedding model returned {embedding.Vector.Length} dimensions, " +
                        $"but the database expects {DocumentChunk.EmbeddingDimensions}.");
                }
                vectors.Add(embedding.Vector);
            }
        }

        return vectors;
    }

    // Se añade al texto que se convierte en vector, pero no al que se guarda. Así un
    // fragmento de "Resolución de problemas" sabe de qué producto es, aunque el nombre
    // del producto solo aparezca en el título del manual.
    private static string ContextHeader(Document document) => document.Product is null
        ? $"Documento: {document.FileName}\n\n"
        : $"Documento: {document.FileName}. Producto: {document.Product.Name}\n\n";
}
