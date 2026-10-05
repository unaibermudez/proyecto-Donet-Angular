using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DocAssist.Api.Domain;
using DocAssist.Api.Features.Documents;
using DocAssist.Api.Features.Products;
using DocAssist.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DocAssist.Api.Tests.Features.Documents;

// Tests de integración de /api/documents: peticiones multipart reales, ficheros en una
// carpeta temporal y filas en un Postgres de Testcontainers.
public class DocumentEndpointsTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Upload_Markdown_Returns201_AndTheSameContentCanBeDownloaded()
    {
        const string text = "# Garantía\n\nTodos los productos tienen dos años de garantía.";

        var response = await UploadAsync("garantia.md", Encoding.UTF8.GetBytes(text));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<DocumentResponse>(Json);
        Assert.NotNull(created);
        Assert.Equal("garantia.md", created.FileName);
        Assert.Equal("text/markdown", created.ContentType);
        Assert.Null(created.ProductId);
        Assert.Equal($"/api/documents/{created.Id}", response.Headers.Location?.ToString());

        var download = await _client.GetAsync($"/api/documents/{created.Id}/content");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("garantia.md", download.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.Equal(text, await download.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Upload_DisallowedExtension_Returns400WithValidationErrors()
    {
        var response = await UploadAsync("notas.txt", "hola"u8.ToArray());

        var problem = await AssertValidationProblemAsync(response);
        Assert.Equal(["Only .md and .pdf files are allowed."], problem.Errors["file"]);
    }

    [Fact]
    public async Task Upload_WithoutFile_Returns400()
    {
        using var form = new MultipartFormDataContent { { new StringContent("1"), "productId" } };

        var response = await _client.PostAsync("/api/documents", form);

        var problem = await AssertValidationProblemAsync(response);
        Assert.Contains("file", problem.Errors.Keys);
    }

    [Fact]
    public async Task Upload_ForUnknownProduct_Returns400_AndStoresNothing()
    {
        var filesBefore = CountStoredFiles();

        var response = await UploadAsync("manual.md", "# Manual"u8.ToArray(), productId: 999_999);

        var problem = await AssertValidationProblemAsync(response);
        Assert.Contains("productId", problem.Errors.Keys);
        Assert.Equal(filesBefore, CountStoredFiles());
    }

    [Fact]
    public async Task Upload_ForProduct_AppearsInTheListFilteredByThatProduct()
    {
        var product = await CreateProductAsync("Consola con manual");
        var manual = await UploadAndReadAsync("manual.pdf", "%PDF-1.7\n%fake"u8.ToArray(), product.Id);
        var general = await UploadAndReadAsync("envios.md", "# Envíos"u8.ToArray());

        var documents = await _client.GetFromJsonAsync<List<DocumentResponse>>(
            $"/api/documents?productId={product.Id}", Json);

        Assert.NotNull(documents);
        var single = Assert.Single(documents);
        Assert.Equal(manual.Id, single.Id);
        Assert.Equal(product.Name, single.ProductName);
        Assert.NotEqual(general.Id, single.Id);
    }

    [Fact]
    public async Task Delete_RemovesTheDocumentAndItsFile()
    {
        var created = await UploadAndReadAsync("devoluciones.md", "# Devoluciones"u8.ToArray());
        var filesBefore = CountStoredFiles();

        var delete = await _client.DeleteAsync($"/api/documents/{created.Id}");
        var getAfterDelete = await _client.GetAsync($"/api/documents/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getAfterDelete.StatusCode);
        Assert.Equal(filesBefore - 1, CountStoredFiles());
    }

    [Fact]
    public async Task DeletingTheProduct_KeepsItsDocumentsAsGeneral()
    {
        var product = await CreateProductAsync("Producto que se va a borrar");
        var manual = await UploadAndReadAsync("manual.md", "# Manual"u8.ToArray(), product.Id);

        var deleteProduct = await _client.DeleteAsync($"/api/products/{product.Id}");
        var read = await _client.GetFromJsonAsync<DocumentResponse>($"/api/documents/{manual.Id}", Json);

        Assert.Equal(HttpStatusCode.NoContent, deleteProduct.StatusCode);
        Assert.NotNull(read);
        Assert.Null(read.ProductId);
        Assert.Null(read.ProductName);
    }

    private async Task<HttpResponseMessage> UploadAsync(string fileName, byte[] content, int? productId = null)
    {
        // Lo mismo que envía el navegador con un <input type="file"> y FormData.
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(content), "file", fileName);
        if (productId is not null)
        {
            form.Add(new StringContent(productId.Value.ToString()), "productId");
        }

        return await _client.PostAsync("/api/documents", form);
    }

    private async Task<DocumentResponse> UploadAndReadAsync(string fileName, byte[] content, int? productId = null)
    {
        var response = await UploadAsync(fileName, content, productId);
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<DocumentResponse>(Json);
        return created ?? throw new InvalidOperationException("La API no devolvió el documento creado.");
    }

    private async Task<ProductResponse> CreateProductAsync(string name)
    {
        var request = new ProductRequest
        {
            Name = name,
            Brand = "Nintendo",
            Model = "Switch 2",
            Category = ProductCategory.Console,
            Price = 469.99m,
            Stock = 3,
            ReleaseDate = new DateOnly(2025, 6, 5)
        };

        var response = await _client.PostAsJsonAsync("/api/products", request, Json);
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<ProductResponse>(Json);
        return created ?? throw new InvalidOperationException("La API no devolvió el producto creado.");
    }

    private static async Task<ValidationProblemDetails> AssertValidationProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Json);
        Assert.NotNull(problem);
        return problem;
    }

    private int CountStoredFiles() =>
        Directory.Exists(factory.StoragePath) ? Directory.GetFiles(factory.StoragePath).Length : 0;
}
