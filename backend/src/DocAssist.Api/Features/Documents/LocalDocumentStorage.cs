using Microsoft.Extensions.Options;

namespace DocAssist.Api.Features.Documents;

// Guarda los documentos en una carpeta del disco local (DocumentStorage:RootPath).
public sealed class LocalDocumentStorage(IOptions<DocumentStorageOptions> options, IHostEnvironment environment)
    : IDocumentStorage
{
    private readonly string _rootPath = Path.GetFullPath(options.Value.RootPath, environment.ContentRootPath);

    public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_rootPath);

        var storedFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        await using var file = File.Create(PathFor(storedFileName));
        await content.CopyToAsync(file, cancellationToken);

        return storedFileName;
    }

    public Stream? OpenRead(string storedFileName)
    {
        var path = PathFor(storedFileName);
        return File.Exists(path) ? File.OpenRead(path) : null;
    }

    public void Delete(string storedFileName) => File.Delete(PathFor(storedFileName));

    // Path.GetFileName descarta cualquier carpeta: el fichero siempre queda dentro de _rootPath.
    private string PathFor(string storedFileName) =>
        Path.Combine(_rootPath, Path.GetFileName(storedFileName));
}
