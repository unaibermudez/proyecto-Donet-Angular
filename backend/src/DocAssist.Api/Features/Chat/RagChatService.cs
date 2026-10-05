using System.Diagnostics;
using System.Runtime.CompilerServices;
using DocAssist.Api.Features.Search;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace DocAssist.Api.Features.Chat;

// RAG completo: busca los fragmentos (paso 9), se los pasa al modelo con las
// instrucciones y devuelve la respuesta con sus citas.
public sealed class RagChatService(
    SemanticSearchService searchService,
    IChatClient chatClient,
    IOptions<RagChatOptions> options,
    ILogger<RagChatService> logger)
{
    public async Task<AskResponse> AskAsync(AskRequest request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var chunks = await RetrieveAsync(request, cancellationToken);
        var citations = RagPrompt.BuildCitations(chunks);

        // Sin documentos procesados no hay nada que leer: no merece la pena esperar al modelo.
        if (chunks.Count == 0)
        {
            return new AskResponse(RagPrompt.NoInformationAnswer, citations, stopwatch.ElapsedMilliseconds);
        }

        var response = await chatClient.GetResponseAsync(
            RagPrompt.BuildMessages(request.Question, chunks), CreateChatOptions(), cancellationToken);

        LogAnswer(chunks.Count, stopwatch, response.Usage?.OutputTokenCount);
        return new AskResponse(response.Text.Trim(), citations, stopwatch.ElapsedMilliseconds);
    }

    // Igual que AskAsync, pero va devolviendo eventos según llegan: primero las fuentes
    // (en cuanto termina la búsqueda) y luego el texto, trozo a trozo, según lo genera el modelo.
    public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(
        AskRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var chunks = await RetrieveAsync(request, cancellationToken);
        yield return new SourcesEvent(RagPrompt.BuildCitations(chunks));

        if (chunks.Count == 0)
        {
            yield return new DeltaEvent(RagPrompt.NoInformationAnswer);
            yield return new DoneEvent(stopwatch.ElapsedMilliseconds);
            yield break;
        }

        var updates = chatClient.GetStreamingResponseAsync(
            RagPrompt.BuildMessages(request.Question, chunks), CreateChatOptions(), cancellationToken);

        long? firstTokenMilliseconds = null;
        await foreach (var update in updates)
        {
            if (string.IsNullOrEmpty(update.Text))
            {
                continue;
            }
            firstTokenMilliseconds ??= stopwatch.ElapsedMilliseconds;
            yield return new DeltaEvent(update.Text);
        }

        logger.LogInformation("First token after {FirstTokenMilliseconds} ms", firstTokenMilliseconds);
        LogAnswer(chunks.Count, stopwatch, outputTokens: null);
        yield return new DoneEvent(stopwatch.ElapsedMilliseconds);
    }

    private Task<List<SearchResult>> RetrieveAsync(AskRequest request, CancellationToken cancellationToken) =>
        searchService.SearchAsync(request.Question, options.Value.ContextChunks, request.ProductId, cancellationToken);

    private ChatOptions CreateChatOptions() => new()
    {
        Temperature = options.Value.Temperature,
        MaxOutputTokens = options.Value.MaxOutputTokens,
    };

    private void LogAnswer(int contextChunks, Stopwatch stopwatch, long? outputTokens) =>
        logger.LogInformation(
            "Answered with {ContextChunks} context chunks in {ElapsedMilliseconds} ms ({OutputTokens} output tokens)",
            contextChunks, stopwatch.ElapsedMilliseconds, outputTokens);
}
