using DocAssist.Api.Domain;
using Microsoft.Extensions.AI;

namespace DocAssist.Api.Tests.Infrastructure;

// Sustituye a Ollama en los tests: devuelve vectores de 768 números calculados a partir
// del texto, siempre los mismos para el mismo texto. No tienen significado, pero
// permiten probar la ingesta sin un modelo real y en milisegundos.
public sealed class FakeEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    // Un texto con esta marca hace fallar la generación, para probar el estado Failed.
    public const string FailureMarker = "FAIL-EMBEDDINGS";

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var embeddings = values.Select(text =>
        {
            if (text.Contains(FailureMarker))
            {
                throw new InvalidOperationException("Fake embedding failure.");
            }
            return new Embedding<float>(VectorFor(text));
        });

        return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(embeddings.ToList()));
    }

    private static float[] VectorFor(string text)
    {
        var seed = text.GetHashCode(StringComparison.Ordinal);
        var random = new Random(seed);
        return Enumerable.Range(0, DocumentChunk.EmbeddingDimensions)
            .Select(_ => random.NextSingle())
            .ToArray();
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
