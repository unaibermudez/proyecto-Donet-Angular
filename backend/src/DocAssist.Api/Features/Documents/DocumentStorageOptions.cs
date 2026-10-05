using System.ComponentModel.DataAnnotations;

namespace DocAssist.Api.Features.Documents;

// Sección "DocumentStorage" de appsettings.json. Se valida al arrancar la aplicación.
public sealed class DocumentStorageOptions
{
    public const string SectionName = "DocumentStorage";

    // Carpeta donde se guardan los ficheros. Si es relativa, se toma desde la
    // carpeta del proyecto (ContentRootPath).
    [Required]
    public string RootPath { get; set; } = "";

    // Como mucho 25: Kestrel rechaza por defecto las peticiones de más de 30 MB
    // (con un 413) antes de que el endpoint llegue a ver el fichero.
    [Range(1, 25)]
    public int MaxFileSizeMegabytes { get; set; } = 10;

    public long MaxFileSizeBytes => MaxFileSizeMegabytes * 1024L * 1024L;
}
