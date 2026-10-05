using System.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;

namespace DocAssist.Api.Features.Search;

public static class SearchEndpoints
{
    private const string LogCategory = "DocAssist.Api.Search";

    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        // POST y no GET: la pregunta va en el cuerpo, puede ser larga y llevar cualquier carácter.
        app.MapPost("/api/search", Search)
            .WithTags("Search")
            .WithSummary("Busca los fragmentos de documentos más parecidos a una pregunta (sin LLM)");

        return app;
    }

    private static async Task<Ok<SearchResponse>> Search(
        SearchRequest request,
        SemanticSearchService searchService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        var results = await searchService.SearchAsync(
            request.Question,
            request.TopK ?? SemanticSearchService.DefaultTopK,
            request.ProductId,
            cancellationToken);

        // La mejor similitud en el log ayuda a ver si la búsqueda encuentra algo útil.
        loggerFactory.CreateLogger(LogCategory).LogInformation(
            "Search returned {ResultCount} results in {ElapsedMilliseconds} ms (best similarity {BestSimilarity})",
            results.Count, stopwatch.ElapsedMilliseconds, results.FirstOrDefault()?.Similarity);

        return TypedResults.Ok(new SearchResponse(request.Question, results, stopwatch.ElapsedMilliseconds));
    }
}
