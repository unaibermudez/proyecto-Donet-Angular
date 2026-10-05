using System.Text.Json.Serialization;
using DocAssist.Api.Data;
using DocAssist.Api.Features.Chat;
using DocAssist.Api.Features.Documents;
using DocAssist.Api.Features.Documents.Ingestion;
using DocAssist.Api.Features.Products;
using DocAssist.Api.Features.Search;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OllamaSharp;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ---------- 1. Registro de servicios (antes de Build) ----------

// Genera el documento OpenAPI a partir de los endpoints definidos.
builder.Services.AddOpenApi();

// Los enums viajan en JSON como texto ("Phone") y no como número (0).
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Si no se puede leer la petición (JSON mal formado...), responder 400 directamente
// en vez de lanzar una excepción. Es lo que ya pasa en producción: así desarrollo
// se comporta igual y un error del cliente no se registra como error del servidor.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);

// Valida automáticamente los parámetros de los endpoints que tengan atributos
// de validación ([Required], [Range]...). Si fallan, responde 400 sin llamar al endpoint.
builder.Services.AddValidation();

// Todos los errores de la API en formato ProblemDetails (RFC 9457).
// "instance" indica qué petición falló, para facilitar la depuración.
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Instance =
            $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}");

// Base de datos: PostgreSQL con nombres de tablas y columnas en snake_case.
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options
        // UseVector: permite mapear las columnas vector de pgvector.
        .UseNpgsql(builder.Configuration.GetConnectionString("Default"), npgsql => npgsql.UseVector())
        .UseSnakeCaseNamingConvention();

    // Datos de ejemplo solo en desarrollo. Se insertan al aplicar las migraciones.
    if (builder.Environment.IsDevelopment())
    {
        options
            .UseSeeding((context, _) => SampleData.Seed(context))
            .UseAsyncSeeding((context, _, cancellationToken) =>
                SampleData.SeedAsync(context, cancellationToken));
    }
});

// Documentos subidos: carpeta y tamaño máximo desde la sección DocumentStorage.
// ValidateOnStart hace que la aplicación no arranque si la configuración no es válida,
// en vez de fallar con la primera subida.
builder.Services.AddOptions<DocumentStorageOptions>()
    .Bind(builder.Configuration.GetSection(DocumentStorageOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<IDocumentStorage, LocalDocumentStorage>();

// Ingesta: troceado (sección Ingestion) y embeddings con Ollama (sección Ollama).
builder.Services.AddOptions<IngestionOptions>()
    .Bind(builder.Configuration.GetSection(IngestionOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<OllamaOptions>()
    .Bind(builder.Configuration.GetSection(OllamaOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// IEmbeddingGenerator es la abstracción de Microsoft.Extensions.AI. OllamaApiClient la
// implementa; para usar OpenAI o Azure OpenAI solo cambiaría esta línea.
builder.Services.AddEmbeddingGenerator<string, Embedding<float>>(services =>
{
    var ollama = services.GetRequiredService<IOptions<OllamaOptions>>().Value;
    return new OllamaApiClient(ollama.Endpoint, ollama.EmbeddingModel);
});

// La cola es singleton (la comparten los endpoints y el worker). El servicio de ingesta
// es scoped porque usa el DbContext. El worker arranca y se para con la aplicación.
builder.Services.AddSingleton<DocumentIngestionQueue>();
builder.Services.AddScoped<DocumentIngestionService>();
builder.Services.AddHostedService<DocumentIngestionWorker>();

// Búsqueda semántica (scoped: usa el DbContext).
builder.Services.AddScoped<SemanticSearchService>();

// Chat con RAG. IChatClient es la otra gran abstracción de Microsoft.Extensions.AI;
// OllamaApiClient también la implementa. Se le da un HttpClient propio para poder
// alargar el tiempo máximo de espera (un modelo en CPU es lento).
builder.Services.AddOptions<RagChatOptions>()
    .Bind(builder.Configuration.GetSection(RagChatOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddChatClient(services =>
{
    var ollama = services.GetRequiredService<IOptions<OllamaOptions>>().Value;
    var httpClient = new HttpClient
    {
        BaseAddress = ollama.Endpoint,
        Timeout = TimeSpan.FromSeconds(ollama.RequestTimeoutSeconds)
    };
    return new OllamaApiClient(httpClient, ollama.ChatModel);
});
builder.Services.AddScoped<RagChatService>();

// La comprobación de la base de datos lleva la etiqueta "ready" para que
// solo la ejecute /health/ready (ver abajo).
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(tags: ["ready"]);

var app = builder.Build();

// ---------- 2. Pipeline y endpoints (después de Build) ----------

// Lo primero del pipeline: cualquier excepción no controlada en lo que viene
// después se convierte en un error con ProblemDetails, sin detalles internos.
// La excepción completa queda en el log, con el mismo traceId.
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    // Si no se pudo leer la petición (JSON mal formado, enum inexistente...),
    // el error es del cliente: se usa el código que trae la excepción (400, 413...)
    // en lugar de un 500.
    StatusCodeSelector = exception => exception is BadHttpRequestException badRequest
        ? badRequest.StatusCode
        : StatusCodes.Status500InternalServerError
});

// Las respuestas de error sin cuerpo (como el NotFound() de los endpoints)
// reciben un ProblemDetails.
app.UseStatusCodePages();

// La documentación de la API solo se expone en desarrollo.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();               // JSON en /openapi/v1.json
    app.MapScalarApiReference();    // interfaz web en /scalar
}

// Liveness: ¿el proceso está vivo? No comprueba dependencias, así que una caída
// de la base de datos no hace que se reinicie la aplicación.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false
});

// Readiness: ¿puede atender peticiones? Comprueba la base de datos.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapGet("/api/info", (IConfiguration config, IHostEnvironment env) =>
        new AppInfoResponse(
            Name: config["App:Name"] ?? "DocAssist",
            Environment: env.EnvironmentName,
            DotnetVersion: Environment.Version.ToString()))
    .WithTags("System")
    .WithSummary("Información básica de la aplicación");

app.MapProductEndpoints();
app.MapDocumentEndpoints();
app.MapSearchEndpoints();
app.MapChatEndpoints();

app.Run();

// ---------- Tipos ----------

record AppInfoResponse(string Name, string Environment, string DotnetVersion);
