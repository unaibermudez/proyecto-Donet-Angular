using System.ComponentModel.DataAnnotations;

namespace DocAssist.Api.Features.Search;

// Lo que el cliente envía a POST /api/search.
public sealed record SearchRequest
{
    [Required, MinLength(3), MaxLength(500)]
    public string Question { get; init; } = "";

    // Cuántos fragmentos devolver. Por defecto, 5.
    [Range(1, 20)]
    public int? TopK { get; init; }

    // Opcional: buscar solo en los documentos de un producto.
    public int? ProductId { get; init; }
}
