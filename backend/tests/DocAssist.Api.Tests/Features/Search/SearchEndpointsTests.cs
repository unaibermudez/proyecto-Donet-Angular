using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DocAssist.Api.Domain;
using DocAssist.Api.Features.Documents;
using DocAssist.Api.Features.Products;
using DocAssist.Api.Features.Search;
using DocAssist.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DocAssist.Api.Tests.Features.Search;

// Tests de integración de POST /api/search. Los embeddings los genera
// FakeEmbeddingGenerator (bolsa de palabras): un fragmento se parece más a la pregunta
// cuantas más palabras comparte con ella. Cada test usa palabras inventadas propias
// ("zarzamora", "kumquat"...) para no depender de lo que suban los demás tests.
public class SearchEndpointsTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Search_ReturnsTheMostSimilarChunkFirst()
    {
        var relevant = await UploadReadyAsync("zarzamora.md",
            "# Zarzamora\n\nLa zarzamora tiene un plazo de devolucion de noventa dias zarzamora.");
        await UploadReadyAsync("arandano.md",
            "# Arandano\n\nEl arandano se envia en cajas de carton reciclado arandano.");

        var response = await SearchAsync(new SearchRequest { Question = "plazo de devolucion de la zarzamora" });

        var best = response.Results[0];
        Assert.Equal(relevant.Id, best.DocumentId);
        Assert.Equal("zarzamora.md", best.FileName);
        Assert.Contains("noventa dias", best.Content);
        Assert.Equal(0, best.ChunkIndex);
    }

    [Fact]
    public async Task Results_AreSortedBySimilarity_AndLimitedToTopK()
    {
        await UploadReadyAsync("kumquat.md", "# Kumquat\n\nKumquat kumquat kumquat garantia.");

        var response = await SearchAsync(new SearchRequest { Question = "garantia del kumquat", TopK = 3 });

        Assert.Equal(3, response.Results.Count);
        var similarities = response.Results.Select(r => r.Similarity).ToList();
        Assert.Equal(similarities.OrderDescending(), similarities);
        Assert.All(similarities, s => Assert.InRange(s, -1, 1));
    }

    [Fact]
    public async Task WithoutTopK_ReturnsFiveResults()
    {
        for (var i = 0; i < 6; i++)
        {
            await UploadReadyAsync($"relleno-{i}.md", $"# Relleno {i}\n\nTexto de relleno numero {i}.");
        }

        var response = await SearchAsync(new SearchRequest { Question = "texto de relleno" });

        Assert.Equal(SemanticSearchService.DefaultTopK, response.Results.Count);
    }

    [Fact]
    public async Task ProductFilter_OnlyReturnsThatProductsDocuments()
    {
        var product = await CreateProductAsync("Consola Pitahaya");
        var manual = await UploadReadyAsync("pitahaya.md", "# Pitahaya\n\nLa pitahaya se carga con USB-C.", product.Id);
        await UploadReadyAsync("pitahaya-general.md", "# Pitahaya general\n\nLa pitahaya se carga con USB-C.");

        var response = await SearchAsync(new SearchRequest
        {
            Question = "como se carga la pitahaya",
            ProductId = product.Id
        });

        var single = Assert.Single(response.Results);
        Assert.Equal(manual.Id, single.DocumentId);
        Assert.Equal(product.Name, single.ProductName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    public async Task TooShortQuestion_Returns400(string question)
    {
        var response = await _client.PostAsJsonAsync("/api/search", new { question }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Json);
        Assert.Contains(problem!.Errors.Keys, key => key.Equals("Question", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TopKOutOfRange_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/search", new { question = "garantia", topK = 50 }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<SearchResponse> SearchAsync(SearchRequest request)
    {
        var response = await _client.PostAsJsonAsync("/api/search", request, Json);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SearchResponse>(Json);
        return result ?? throw new InvalidOperationException("La API no devolvió resultados.");
    }

    // Sube un documento y espera a que el worker lo procese.
    private async Task<DocumentResponse> UploadReadyAsync(string fileName, string content, int? productId = null)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(content)), "file", fileName);
        if (productId is not null)
        {
            form.Add(new StringContent(productId.Value.ToString()), "productId");
        }

        var response = await _client.PostAsync("/api/documents", form);
        response.EnsureSuccessStatusCode();
        var document = await response.Content.ReadFromJsonAsync<DocumentResponse>(Json)
            ?? throw new InvalidOperationException("La API no devolvió el documento creado.");

        var timeout = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < timeout)
        {
            var current = await _client.GetFromJsonAsync<DocumentResponse>($"/api/documents/{document.Id}", Json);
            if (current?.Status == DocumentStatus.Ready)
            {
                return current;
            }
            await Task.Delay(100);
        }
        throw new TimeoutException($"El documento {fileName} no se procesó a tiempo.");
    }

    private async Task<ProductResponse> CreateProductAsync(string name)
    {
        var request = new ProductRequest
        {
            Name = name,
            Brand = "Frutal",
            Model = "P1",
            Category = ProductCategory.Console,
            Price = 199.99m,
            Stock = 1,
            ReleaseDate = new DateOnly(2025, 1, 1)
        };

        var response = await _client.PostAsJsonAsync("/api/products", request, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductResponse>(Json))!;
    }
}
