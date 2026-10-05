namespace DocAssist.Api.Features.Documents;

// Qué ficheros se pueden subir. Código puro, sin HTTP ni disco, para poder probarlo
// con tests unitarios.
public static class DocumentFileRules
{
    // Extensiones permitidas y el Content-Type con el que se guardan. El que manda el
    // navegador no se usa: lo decide el cliente y no es fiable.
    public static readonly IReadOnlyDictionary<string, string> ContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".md"] = "text/markdown",
            [".pdf"] = "application/pdf",
        };

    // Cuántos bytes del principio del fichero hacen falta para comprobar su contenido.
    public const int HeaderLength = 512;

    public const int MaxFileNameLength = 255;

    // Todo PDF empieza por "%PDF-".
    private static ReadOnlySpan<byte> PdfSignature => "%PDF-"u8;

    // Devuelve el motivo por el que el fichero no es válido, o null si lo es.
    // header son los primeros bytes del fichero (como mucho HeaderLength).
    public static string? Validate(string fileName, long length, ReadOnlySpan<byte> header, long maxBytes)
    {
        var extension = Path.GetExtension(fileName);

        if (!ContentTypes.ContainsKey(extension))
        {
            return "Only .md and .pdf files are allowed.";
        }

        if (fileName.Length > MaxFileNameLength)
        {
            return $"The file name cannot be longer than {MaxFileNameLength} characters.";
        }

        if (length == 0)
        {
            return "The file is empty.";
        }

        if (length > maxBytes)
        {
            return $"The file cannot be larger than {maxBytes / 1024 / 1024} MB.";
        }

        // La extensión la elige el usuario. Se mira también el contenido, para que un
        // ejecutable renombrado a .pdf no pase.
        if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase) && !header.StartsWith(PdfSignature))
        {
            return "The file is not a valid PDF.";
        }

        // Un fichero de texto no tiene bytes a cero; uno binario casi siempre los tiene.
        if (extension.Equals(".md", StringComparison.OrdinalIgnoreCase) && header.Contains((byte)0))
        {
            return "The file is not a text file.";
        }

        return null;
    }
}
