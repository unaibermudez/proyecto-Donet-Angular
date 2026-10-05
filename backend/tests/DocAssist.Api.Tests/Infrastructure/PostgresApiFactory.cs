using DocAssist.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace DocAssist.Api.Tests.Infrastructure;

// Arranca la API completa en memoria contra un Postgres real y desechable en Docker.
// Se crea una vez por clase de tests (IClassFixture) y se destruye al terminar.
public sealed class PostgresApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // La misma imagen que docker-compose.yml, para probar contra el mismo Postgres.
    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();

    // Carpeta temporal para los documentos subidos en los tests. Se borra al terminar.
    public string StoragePath { get; } =
        Path.Combine(Path.GetTempPath(), "docassist-tests", Guid.NewGuid().ToString("N"));

    // El modelo de chat falso. Público para que los tests vean qué prompt recibió.
    public FakeChatClient ChatClient { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Sustituye la cadena de conexión de appsettings.Development.json por la del
        // contenedor. Se añade la última, así que tiene prioridad sobre las demás.
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _postgres.GetConnectionString(),
                ["DocumentStorage:RootPath"] = StoragePath
            }));

        // Los tests no dependen de Ollama: los embeddings los genera un sustituto.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmbeddingGenerator<string, Embedding<float>>>();
            services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>, FakeEmbeddingGenerator>();

            // Ni del LLM: el chat responde con FakeChatClient.
            services.RemoveAll<IChatClient>();
            services.AddSingleton<IChatClient>(ChatClient);
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Crea las tablas con las migraciones reales (y ejecuta el seed de desarrollo).
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    // Implementación explícita: WebApplicationFactory ya tiene un DisposeAsync
    // que devuelve ValueTask, y el de xUnit devuelve Task.
    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();

        if (Directory.Exists(StoragePath))
        {
            Directory.Delete(StoragePath, recursive: true);
        }
    }
}
