using DocAssist.Api.Features.Documents.Ingestion;

namespace DocAssist.Api.Tests.Features.Documents.Ingestion;

// Tests unitarios del troceador: código puro, sin IA ni base de datos.
public class TextChunkerTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   \n\n  \r\n ")]
    public void EmptyText_HasNoChunks(string text)
    {
        Assert.Empty(TextChunker.Split(text, maxLength: 200, overlap: 50));
    }

    [Fact]
    public void ShortText_IsASingleChunk()
    {
        var chunks = TextChunker.Split("  # Garantía\n\nTres años.  ", maxLength: 200, overlap: 50);

        Assert.Equal(["# Garantía\n\nTres años."], chunks);
    }

    [Fact]
    public void WholeParagraphsAreKeptTogetherWhileTheyFit()
    {
        var first = Paragraph('a', 300);
        var second = Paragraph('b', 300);
        var third = Paragraph('c', 300);

        var chunks = TextChunker.Split($"{first}\n\n{second}\n\n{third}", maxLength: 700, overlap: 0);

        Assert.Equal([$"{first}\n\n{second}", third], chunks);
    }

    [Fact]
    public void WindowsLineEndings_SeparateParagraphsToo()
    {
        var chunks = TextChunker.Split($"{Paragraph('a', 150)}\r\n\r\n{Paragraph('b', 150)}", maxLength: 200, overlap: 0);

        Assert.Equal(2, chunks.Count);
    }

    [Theory]
    [InlineData(200, 0)]
    [InlineData(200, 50)]
    [InlineData(1000, 200)]
    [InlineData(1000, 500)]
    public void NoChunkIsLongerThanTheMaximum(int maxLength, int overlap)
    {
        var chunks = TextChunker.Split(LongText(), maxLength, overlap);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, chunk => Assert.InRange(chunk.Length, 1, maxLength));
    }

    [Fact]
    public void EachChunkStartsWithTheEndOfThePreviousOne()
    {
        var chunks = TextChunker.Split(LongText(), maxLength: 300, overlap: 80);

        for (var i = 1; i < chunks.Count; i++)
        {
            var previous = chunks[i - 1];
            var firstWords = string.Join(' ', chunks[i].Split(' ').Take(3));
            Assert.Contains(firstWords, previous[^100..]);
        }
    }

    [Fact]
    public void NoWordIsLost()
    {
        var text = LongText();

        var chunks = TextChunker.Split(text, maxLength: 250, overlap: 60);

        var wordsInChunks = chunks.SelectMany(Words).ToHashSet();
        Assert.All(Words(text), word => Assert.Contains(word, wordsInChunks));
    }

    [Fact]
    public void WordsAreNotCutInHalf()
    {
        var text = LongText();
        var originalWords = Words(text).ToHashSet();

        var chunks = TextChunker.Split(text, maxLength: 250, overlap: 60);

        Assert.All(chunks.SelectMany(Words), word => Assert.Contains(word, originalWords));
    }

    [Fact]
    public void AWordLongerThanAChunk_IsSplitByForce()
    {
        var url = "https://example.com/" + new string('x', 500);

        var chunks = TextChunker.Split(url, maxLength: 200, overlap: 0);

        Assert.True(chunks.Count >= 3);
        Assert.Equal(url, string.Concat(chunks));
    }

    [Theory]
    [InlineData(99, 0)]
    [InlineData(200, -1)]
    [InlineData(200, 101)]
    public void InvalidSizes_Throw(int maxLength, int overlap)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TextChunker.Split("texto", maxLength, overlap));
    }

    // Un párrafo de exactamente `length` caracteres: palabras de 9 letras separadas por espacios.
    private static string Paragraph(char letter, int length) =>
        string.Join(' ', Enumerable.Repeat(new string(letter, 9), length / 10 + 1))[..length].TrimEnd();

    // Texto con párrafos de distintos tamaños y palabras distintas, uno de ellos muy largo.
    private static string LongText() => string.Join("\n\n", Enumerable.Range(1, 12).Select(p =>
        string.Join(' ', Enumerable.Range(1, p * 9).Select(w => $"p{p}w{w}"))));

    private static IEnumerable<string> Words(string text) =>
        text.Split([' ', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
}
