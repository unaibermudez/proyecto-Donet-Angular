using System.Text.RegularExpressions;
using DocAssist.Api.Domain;
using Microsoft.Extensions.AI;

namespace DocAssist.Api.Tests.Infrastructure;

// Sustituye a Ollama en los tests. Genera vectores de 768 números con la técnica de
// "bolsa de palabras": cada palabra suma 1 en una posición fija del vector (según su
// hash). Así, dos textos que comparten palabras dan vectores cercanos, que es lo que
// necesitan los tests de búsqueda para comprobar el orden de los resultados.
// No entiende sinónimos como un modelo real, pero es determinista e instantáneo.
public sealed partial class FakeEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
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
        var vector = new float[DocumentChunk.EmbeddingDimensions];
        foreach (Match word in Words().Matches(text.ToLowerInvariant()))
        {
            var position = (int)((uint)StableHash(word.Value) % DocumentChunk.EmbeddingDimensions);
            vector[position] += 1;
        }

        // Un vector de ceros no tiene dirección y la distancia coseno no está definida.
        if (vector.All(v => v == 0))
        {
            vector[0] = 1;
        }
        return vector;
    }

    // string.GetHashCode cambia en cada ejecución; este hash (FNV-1a) siempre da lo mismo.
    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = (int)2166136261;
            foreach (var character in value)
            {
                hash = (hash ^ character) * 16777619;
            }
            return hash;
        }
    }

    [GeneratedRegex(@"\p{L}+|\d+")]
    private static partial Regex Words();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
