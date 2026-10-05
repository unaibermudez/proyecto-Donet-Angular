namespace DocAssist.Api.Domain;

// Un producto del catálogo de la tienda (móvil, ordenador o consola).
public class Product
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public required string Brand { get; set; }

    public required string Model { get; set; }

    public ProductCategory Category { get; set; }

    // decimal y no double: con dinero no se admiten errores de redondeo.
    public decimal Price { get; set; }

    public int Stock { get; set; }

    // Fecha de lanzamiento. DateOnly porque no nos interesa la hora.
    public DateOnly ReleaseDate { get; set; }

    // Especificaciones opcionales: no aplican igual a todos los productos
    // (una PS5 no tiene pantalla, una Nintendo Switch sí), así que pueden ser nulas.
    public int? RamGb { get; set; }

    public int? StorageGb { get; set; }

    public decimal? ScreenInches { get; set; }

    // Manuales y otros documentos de este producto (relación uno a muchos).
    public List<Document> Documents { get; } = [];
}
