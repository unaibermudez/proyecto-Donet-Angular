using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DocAssist.Api.Data;
using DocAssist.Api.Domain;
using DocAssist.Api.Features.Documents;
using DocAssist.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocAssist.Api.Tests.Features.Documents.Ingestion;

// Tests de integración de la ingesta: subida → worker en segundo plano → fragmentos con
// su vector en Postgres (pgvector). Los embeddings los genera FakeEmbeddingGenerator.
public class DocumentIngestionTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task UploadedDocument_IsChunkedAndEmbedded_InTheBackground()
    {
        var uploaded = await UploadAsync("manual.md", LongMarkdown());
        Assert.Equal(DocumentStatus.Pending, uploaded.Status);

        var ready = await WaitUntilFinishedAsync(uploaded.Id);

        Assert.Equal(DocumentStatus.Ready, ready.Status);
        Assert.Null(ready.StatusMessage);

        var chunks = await _client.GetFromJsonAsync<List<DocumentChunkResponse>>(
            $"/api/documents/{uploaded.Id}/chunks", Json);
        Assert.NotNull(chunks);
        Assert.True(chunks.Count > 1);
        Assert.Equal(Enumerable.Range(0, chunks.Count), chunks.Select(c => c.Index));
        Assert.All(chunks, chunk => Assert.InRange(chunk.Length, 1, 1000));
        Assert.StartsWith("# Manual de prueba", chunks[0].Content);
    }

    [Fact]
    public async Task EveryChunk_HasA768DimensionVectorInPostgres()
    {
        var uploaded = await UploadAsync("garantia.md", LongMarkdown());
        await WaitUntilFinishedAsync(uploaded.Id);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var embeddings = await db.DocumentChunks
            .Where(c => c.DocumentId == uploaded.Id)
            .Select(c => c.Embedding)
            .ToListAsync();

        Assert.NotEmpty(embeddings);
        Assert.All(embeddings, e => Assert.Equal(DocumentChunk.EmbeddingDimensions, e.ToArray().Length));
    }

    [Fact]
    public async Task WhenEmbeddingsFail_TheDocumentIsMarkedAsFailedWithTheReason()
    {
        var uploaded = await UploadAsync("roto.md", $"# Documento\n\n{FakeEmbeddingGenerator.FailureMarker}");

        var failed = await WaitUntilFinishedAsync(uploaded.Id);

        Assert.Equal(DocumentStatus.Failed, failed.Status);
        Assert.Equal("Fake embedding failure.", failed.StatusMessage);
    }

    [Fact]
    public async Task Reingesting_ReplacesTheChunksInsteadOfDuplicatingThem()
    {
        var uploaded = await UploadAsync("envios.md", LongMarkdown());
        await WaitUntilFinishedAsync(uploaded.Id);
        var before = await CountChunksAsync(uploaded.Id);

        var response = await _client.PostAsync($"/api/documents/{uploaded.Id}/ingest", null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var accepted = await response.Content.ReadFromJsonAsync<DocumentResponse>(Json);
        Assert.Equal(DocumentStatus.Pending, accepted?.Status);

        var ready = await WaitUntilFinishedAsync(uploaded.Id);
        Assert.Equal(DocumentStatus.Ready, ready.Status);
        Assert.Equal(before, await CountChunksAsync(uploaded.Id));
    }

    [Fact]
    public async Task Ingest_UnknownDocument_Returns404()
    {
        var response = await _client.PostAsync("/api/documents/999999/ingest", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeletingTheDocument_DeletesItsChunks()
    {
        var uploaded = await UploadAsync("devoluciones.md", LongMarkdown());
        await WaitUntilFinishedAsync(uploaded.Id);

        await _client.DeleteAsync($"/api/documents/{uploaded.Id}");

        Assert.Equal(0, await CountChunksAsync(uploaded.Id));
    }

    private async Task<DocumentResponse> UploadAsync(string fileName, string content)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(content)), "file", fileName);

        var response = await _client.PostAsync("/api/documents", form);
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<DocumentResponse>(Json);
        return created ?? throw new InvalidOperationException("La API no devolvió el documento creado.");
    }

    // La ingesta va en segundo plano: se consulta el estado hasta que termina.
    private async Task<DocumentResponse> WaitUntilFinishedAsync(int documentId)
    {
        var timeout = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < timeout)
        {
            var document = await _client.GetFromJsonAsync<DocumentResponse>($"/api/documents/{documentId}", Json);
            if (document?.Status is DocumentStatus.Ready or DocumentStatus.Failed)
            {
                return document;
            }
            await Task.Delay(100);
        }

        throw new TimeoutException($"El documento {documentId} no terminó de procesarse en 15 segundos.");
    }

    private async Task<int> CountChunksAsync(int documentId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.DocumentChunks.CountAsync(c => c.DocumentId == documentId);
    }

    // Unos 3.000 caracteres en párrafos, para que salgan varios fragmentos.
    private static string LongMarkdown() =>
        "# Manual de prueba\n\n" + string.Join("\n\n", Enumerable.Range(1, 20).Select(i =>
            $"## Sección {i}\n\nEste es el párrafo número {i} del manual, con instrucciones de uso " +
            $"y consejos de mantenimiento para que el producto dure muchos años."));
}
