using DocAssist.Api.Data;
using DocAssist.Api.Domain;
using DocAssist.Api.Features.Documents.Ingestion;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DocAssist.Api.Features.Documents;

public static class DocumentEndpoints
{
    private const string LogCategory = "DocAssist.Api.Documents";

    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/documents").WithTags("Documents");

        group.MapGet("/", GetDocuments)
            .WithSummary("Lista los documentos, opcionalmente solo los de un producto");

        group.MapGet("/{id:int}", GetDocumentById)
            .WithSummary("Obtiene los datos de un documento");

        group.MapGet("/{id:int}/content", DownloadDocument)
            .WithSummary("Descarga el fichero de un documento");

        // Los endpoints que leen formularios exigen por defecto un token antiforgery,
        // que protege de CSRF a las aplicaciones que se autentican con cookies. Esta API
        // no usa cookies, así que no aplica.
        group.MapPost("/", UploadDocument)
            .DisableAntiforgery()
            .WithSummary("Sube un documento (.md o .pdf) como multipart/form-data");

        group.MapPost("/{id:int}/ingest", IngestDocument)
            .WithSummary("Vuelve a procesar un documento: troceado y embeddings");

        group.MapGet("/{id:int}/chunks", GetDocumentChunks)
            .WithSummary("Lista los fragmentos en los que se ha troceado un documento");

        group.MapDelete("/{id:int}", DeleteDocument)
            .WithSummary("Borra un documento y su fichero");

        return app;
    }

    private static async Task<Ok<List<DocumentResponse>>> GetDocuments(
        int? productId,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var query = db.Documents.AsNoTracking().Include(d => d.Product).AsQueryable();

        if (productId is not null)
        {
            query = query.Where(d => d.ProductId == productId);
        }

        var documents = await query
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(documents.Select(DocumentResponse.FromEntity).ToList());
    }

    private static async Task<Results<Ok<DocumentResponse>, NotFound>> GetDocumentById(
        int id,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var document = await db.Documents
            .AsNoTracking()
            .Include(d => d.Product)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        return document is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(DocumentResponse.FromEntity(document));
    }

    private static async Task<Results<FileStreamHttpResult, NotFound>> DownloadDocument(
        int id,
        AppDbContext db,
        IDocumentStorage storage,
        CancellationToken cancellationToken)
    {
        var document = await db.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        var content = document is null ? null : storage.OpenRead(document.StoredFileName);
        if (document is null || content is null)
        {
            return TypedResults.NotFound();
        }

        // Con fileDownloadName, el navegador lo descarga con su nombre original.
        // ASP.NET Core cierra el stream al terminar de enviarlo.
        return TypedResults.File(content, document.ContentType, document.FileName);
    }

    private static async Task<Results<Created<DocumentResponse>, ValidationProblem>> UploadDocument(
        IFormFile? file,
        [FromForm] int? productId,
        AppDbContext db,
        IDocumentStorage storage,
        IOptions<DocumentStorageOptions> options,
        DocumentIngestionQueue ingestionQueue,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        // Mismo formato que los errores de validación de los productos:
        // un ValidationProblemDetails con los mensajes agrupados por campo.
        var errors = new Dictionary<string, string[]>();

        if (file is null)
        {
            errors["file"] = ["A file is required."];
        }
        else
        {
            var header = new byte[DocumentFileRules.HeaderLength];
            int headerLength;
            await using (var stream = file.OpenReadStream())
            {
                headerLength = await stream.ReadAtLeastAsync(
                    header, header.Length, throwOnEndOfStream: false, cancellationToken);
            }

            var fileError = DocumentFileRules.Validate(
                file.FileName, file.Length, header.AsSpan(0, headerLength), options.Value.MaxFileSizeBytes);
            if (fileError is not null)
            {
                errors["file"] = [fileError];
            }
        }

        Product? product = null;
        if (productId is not null)
        {
            product = await db.Products.FindAsync([productId.Value], cancellationToken);
            if (product is null)
            {
                errors["productId"] = [$"The product {productId} does not exist."];
            }
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        // file no es null aquí: si lo fuera, habría un error y ya habríamos salido.
        var extension = Path.GetExtension(file!.FileName).ToLowerInvariant();
        string storedFileName;
        await using (var stream = file.OpenReadStream())
        {
            storedFileName = await storage.SaveAsync(stream, extension, cancellationToken);
        }

        var document = new Document
        {
            // GetFileName quita la ruta que algunos navegadores antiguos incluyen.
            FileName = Path.GetFileName(file.FileName),
            ContentType = DocumentFileRules.ContentTypes[extension],
            SizeBytes = file.Length,
            StoredFileName = storedFileName,
            UploadedAt = DateTime.UtcNow,
            Product = product,
        };
        db.Documents.Add(document);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Sin fila en la base de datos, el fichero quedaría huérfano en el disco.
            storage.Delete(storedFileName);
            throw;
        }

        loggerFactory.CreateLogger(LogCategory).LogInformation(
            "Document {DocumentId} uploaded: {FileName} ({SizeBytes} bytes, product {ProductId})",
            document.Id, document.FileName, document.SizeBytes, document.ProductId);

        // La ingesta sigue en segundo plano: la respuesta sale ya, con estado Pending.
        ingestionQueue.Enqueue(document.Id);

        return TypedResults.Created($"/api/documents/{document.Id}", DocumentResponse.FromEntity(document));
    }

    private static async Task<Results<Accepted<DocumentResponse>, NotFound, ProblemHttpResult>> IngestDocument(
        int id,
        AppDbContext db,
        DocumentIngestionQueue ingestionQueue,
        CancellationToken cancellationToken)
    {
        var document = await db.Documents
            .Include(d => d.Product)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (document is null)
        {
            return TypedResults.NotFound();
        }

        if (document.Status is DocumentStatus.Pending or DocumentStatus.Processing)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The document is already waiting to be ingested or being ingested.");
        }

        document.Status = DocumentStatus.Pending;
        document.StatusMessage = null;
        await db.SaveChangesAsync(cancellationToken);
        ingestionQueue.Enqueue(document.Id);

        // 202 Accepted: la petición se ha aceptado, pero el trabajo todavía no está hecho.
        // El estado se consulta en la URL del documento.
        return TypedResults.Accepted($"/api/documents/{document.Id}", DocumentResponse.FromEntity(document));
    }

    private static async Task<Results<Ok<List<DocumentChunkResponse>>, NotFound>> GetDocumentChunks(
        int id,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        if (!await db.Documents.AnyAsync(d => d.Id == id, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        // Select: solo las columnas que hacen falta, sin traer los vectores.
        var chunks = await db.DocumentChunks
            .Where(c => c.DocumentId == id)
            .OrderBy(c => c.Index)
            .Select(c => new DocumentChunkResponse(c.Index, c.Content, c.Content.Length))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(chunks);
    }

    private static async Task<Results<NoContent, NotFound>> DeleteDocument(
        int id,
        AppDbContext db,
        IDocumentStorage storage,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var document = await db.Documents.FindAsync([id], cancellationToken);
        if (document is null)
        {
            return TypedResults.NotFound();
        }

        // Primero la fila y después el fichero: si fallara el borrado del fichero,
        // queda un fichero sin usar, que es mejor que una fila que apunta a la nada.
        db.Documents.Remove(document);
        await db.SaveChangesAsync(cancellationToken);
        storage.Delete(document.StoredFileName);

        loggerFactory.CreateLogger(LogCategory).LogInformation("Document {DocumentId} deleted", id);

        return TypedResults.NoContent();
    }
}
