using System.Text;
using System.Text.RegularExpressions;

namespace DocAssist.Api.Features.Documents.Ingestion;

// Trocea un texto en fragmentos (chunks) de como mucho maxLength caracteres, con
// solapamiento entre fragmentos seguidos. Código puro, sin IA: se prueba con tests unitarios.
//
// - Respeta los párrafos: junta párrafos enteros mientras quepan, para no cortar una
//   idea por la mitad. Solo parte un párrafo si él solo no cabe, y entonces por palabras.
// - Solapamiento: cada fragmento empieza con el final del anterior (overlap caracteres,
//   sin cortar palabras). Así una frase que cae en la frontera aparece entera en alguno.
public static partial class TextChunker
{
    private const string Separator = "\n\n";

    public static List<string> Split(string text, int maxLength, int overlap)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, 100);
        ArgumentOutOfRangeException.ThrowIfNegative(overlap);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(overlap, maxLength / 2);

        // Cada trozo deja sitio para el solapamiento y el separador: así, al empezar un
        // fragmento nuevo con el final del anterior, nunca se pasa de maxLength.
        var pieceLength = maxLength - overlap - Separator.Length;
        var pieces = SplitIntoParagraphs(text).SelectMany(p => SplitLongParagraph(p, pieceLength));

        var chunks = new List<string>();
        var current = new StringBuilder();

        foreach (var piece in pieces)
        {
            if (current.Length > 0 && current.Length + Separator.Length + piece.Length > maxLength)
            {
                var finished = current.ToString();
                chunks.Add(finished);
                current.Clear().Append(Tail(finished, overlap));
            }

            if (current.Length > 0)
            {
                current.Append(Separator);
            }
            current.Append(piece);
        }

        if (current.Length > 0)
        {
            chunks.Add(current.ToString());
        }

        return chunks;
    }

    // Los párrafos están separados por al menos una línea en blanco.
    private static IEnumerable<string> SplitIntoParagraphs(string text) =>
        BlankLines().Split(text.ReplaceLineEndings("\n"))
            .Select(p => p.Trim())
            .Where(p => p.Length > 0);

    // Un párrafo que no cabe se parte por palabras; una palabra que no cabe (raro: una
    // URL enorme), a la fuerza.
    private static IEnumerable<string> SplitLongParagraph(string paragraph, int limit)
    {
        if (paragraph.Length <= limit)
        {
            yield return paragraph;
            yield break;
        }

        var current = new StringBuilder();
        foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.Length > 0 && current.Length + 1 + word.Length > limit)
            {
                yield return current.ToString();
                current.Clear();
            }

            if (word.Length > limit)
            {
                for (var start = 0; start < word.Length; start += limit)
                {
                    yield return word.Substring(start, Math.Min(limit, word.Length - start));
                }
                continue;
            }

            if (current.Length > 0)
            {
                current.Append(' ');
            }
            current.Append(word);
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }

    // Los últimos `length` caracteres del texto, empezando en una palabra completa.
    private static string Tail(string text, int length)
    {
        if (length == 0)
        {
            return "";
        }
        if (text.Length <= length)
        {
            return text;
        }

        var start = text.Length - length;
        var wordStart = text.IndexOfAny([' ', '\n'], start);
        return wordStart == -1 ? text[start..] : text[(wordStart + 1)..].TrimStart();
    }

    [GeneratedRegex(@"\n\s*\n")]
    private static partial Regex BlankLines();
}
