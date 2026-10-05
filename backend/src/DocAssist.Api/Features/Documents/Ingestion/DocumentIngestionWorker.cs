using DocAssist.Api.Data;
using DocAssist.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace DocAssist.Api.Features.Documents.Ingestion;

// Servicio en segundo plano que procesa los documentos de la cola, de uno en uno.
// Arranca y se para con la aplicación. Generar embeddings con Ollama en CPU es lento,
// así que la subida responde al momento y la ingesta sigue aquí, sin bloquear la petición.
public sealed class DocumentIngestionWorker(
    DocumentIngestionQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<DocumentIngestionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnqueueUnfinishedDocumentsAsync(stoppingToken);

        await foreach (var documentId in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                // Un BackgroundService es singleton y el DbContext es scoped: se crea un
                // scope por documento, igual que ASP.NET Core crea uno por petición.
                using var scope = scopeFactory.CreateScope();
                var ingestion = scope.ServiceProvider.GetRequiredService<DocumentIngestionService>();
                await ingestion.IngestAsync(documentId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // Un error con un documento no puede parar el worker para los demás.
                logger.LogError(exception, "Unexpected error ingesting document {DocumentId}", documentId);
            }
        }
    }

    // Lo que quedó a medias la última vez que se paró la aplicación.
    private async Task EnqueueUnfinishedDocumentsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var documentIds = await db.Documents
                .Where(d => d.Status == DocumentStatus.Pending || d.Status == DocumentStatus.Processing)
                .OrderBy(d => d.Id)
                .Select(d => d.Id)
                .ToListAsync(cancellationToken);

            foreach (var documentId in documentIds)
            {
                queue.Enqueue(documentId);
            }

            if (documentIds.Count > 0)
            {
                logger.LogInformation("{Count} unfinished documents queued for ingestion", documentIds.Count);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Por ejemplo, si la base de datos todavía no está lista. Los documentos se
            // pueden volver a procesar con POST /api/documents/{id}/ingest.
            logger.LogWarning(exception, "Could not look for documents pending ingestion");
        }
    }
}
