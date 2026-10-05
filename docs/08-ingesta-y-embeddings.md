# 08 · Ingesta: troceado, embeddings y pgvector

## Qué hemos hecho

Cada documento subido se convierte en fragmentos con su vector, listos para buscar
por significado. Es el corazón del RAG:

```
Subida (POST /api/documents)
   │  responde al momento: 201, estado Pending
   ▼
Cola en memoria (Channel) ──▶ Worker en segundo plano (BackgroundService)
                                 │ 1. Estado Processing
                                 │ 2. Texto: Markdown tal cual, PDF con PdfPig
                                 │ 3. Troceado: ~1.000 caracteres, 200 de solapamiento
                                 │ 4. Embeddings con Ollama (nomic-embed-text, 768 números)
                                 │ 5. Fragmentos + vectores en document_chunks (pgvector)
                                 ▼
                              Estado Ready (o Failed con el motivo)
```

**Backend:**

- **Tabla `document_chunks`**: texto del fragmento, posición y columna
  **`vector(768)`** con índice **HNSW** por distancia coseno. Se borran con su documento.
- **Estado del documento** (`Pending`, `Processing`, `Ready`, `Failed`) y mensaje de error.
- **`TextChunker`**: troceador propio, código puro con 16 tests unitarios.
- **`DocumentTextExtractor`**: texto de Markdown y PDF.
- **`IEmbeddingGenerator`** de `Microsoft.Extensions.AI`, implementado por OllamaSharp.
- **Cola + worker**: la ingesta no bloquea la subida. Al arrancar, el worker vuelve a
  encolar lo que quedó a medias.
- **Endpoints nuevos**:

| Método | Ruta | Qué hace | Respuestas |
|---|---|---|---|
| `POST` | `/api/documents/{id}/ingest` | Vuelve a procesar un documento | 202 + `Location`, 404, 409 si ya está en proceso |
| `GET` | `/api/documents/{id}/chunks` | Fragmentos del documento (sin vectores), para ver cómo se ha troceado | 200, 404 |

- **Migración `AddDocumentChunks`**, aplicada. Los 13 documentos de ejemplo ya están
  procesados: **37 fragmentos** en unos 22 segundos con Ollama en CPU.

**Frontend** (`/documents`): columna **Estado** con etiqueta de color, mensaje de
error si falla, botón **Reprocesar** y actualización automática de la lista cada
2 segundos mientras haya documentos sin terminar.

**Tests**: 66 en el backend (+25) y 32 en el frontend (+3).

## Conceptos nuevos

### Embeddings

Un **embedding** es una lista de números (aquí 768) que representa el **significado**
de un texto. Textos que dicen cosas parecidas dan vectores cercanos, aunque no
compartan palabras:

```
"¿Cuántos días tengo para devolver un producto?"   ─┐
                                                    ├─ vectores cercanos
"Tienes 30 días naturales desde la entrega..."      ─┘
```

Por eso el RAG puede encontrar la respuesta aunque el usuario no use las mismas
palabras que el documento. Lo genera un modelo (aquí `nomic-embed-text` en Ollama),
no el LLM que responde.

**Similitud coseno**: mide el ángulo entre dos vectores. 1 = mismo significado,
0 = nada que ver. pgvector la calcula con el operador `<=>` (que devuelve la
*distancia*, es decir, 1 − similitud).

### Por qué trocear (chunking)

- Un embedding de un documento entero es una **media** de todos sus temas: no se
  parece mucho a ninguna pregunta concreta.
- Al LLM se le pasarán solo los fragmentos relevantes, no los documentos enteros:
  menos tokens, más rápido y respuestas más centradas.

**Tamaño**: ~1.000 caracteres (250-300 tokens). Más pequeño pierde contexto; más
grande diluye el significado.

**Solapamiento** (200 caracteres): cada fragmento empieza con el final del anterior.
Si una frase importante cae justo en la frontera, aparece entera en alguno de los dos.

```
Fragmento 0: [··········································]
Fragmento 1:                              [·····································]
                                          └ solapamiento ┘
```

El troceador **respeta los párrafos**: junta párrafos enteros mientras quepan y solo
parte uno por palabras si él solo no cabe. Nunca corta una palabra (salvo una más
larga que un fragmento, como una URL enorme).

### Mejoras al texto que se convierte en vector

Lo que se **guarda** es el fragmento tal cual (es lo que verá el LLM). Lo que se
**convierte en vector** lleva dos añadidos:

```
search_document: Documento: manual-asus-rog-strix-g16.md. Producto: ASUS ROG Strix G16

16 GB de RAM DDR5 en dos módulos, ampliables hasta 32 GB...
```

- **`search_document: `**: `nomic-embed-text` está entrenado con este prefijo para los
  textos que se guardan (y `search_query: ` para las preguntas, en el paso 9). Es
  configurable porque depende del modelo.
- **Documento y producto**: un fragmento de "Resolución de problemas" no dice de qué
  producto es; con la cabecera, su vector sí lo sabe.

### `Microsoft.Extensions.AI`

Abstracciones comunes para trabajar con modelos de IA en .NET, sin atarse a un proveedor:

```csharp
// Program.cs: el único sitio que sabe que es Ollama.
builder.Services.AddEmbeddingGenerator<string, Embedding<float>>(services =>
    new OllamaApiClient(ollama.Endpoint, ollama.EmbeddingModel));

// DocumentIngestionService: solo conoce la interfaz.
IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator
var embeddings = await embeddingGenerator.GenerateAsync(textos);
```

*Es el equivalente a Spring AI (`EmbeddingModel`) o a LangChain.* Para usar Azure
OpenAI bastaría con cambiar la línea del registro. En el paso 10 se usará
`IChatClient`, de la misma librería, para el chat.

### pgvector en EF Core

```csharp
// Entidad
public required Vector Embedding { get; set; }      // tipo Pgvector.Vector

// Configuración
builder.Property(c => c.Embedding).HasColumnType("vector(768)");
builder.HasIndex(c => c.Embedding)
    .HasMethod("hnsw")
    .HasOperators("vector_cosine_ops");

// Program.cs
options.UseNpgsql(connectionString, npgsql => npgsql.UseVector());

// AppDbContext: la migración incluye CREATE EXTENSION IF NOT EXISTS vector
modelBuilder.HasPostgresExtension("vector");
```

**HNSW** (*Hierarchical Navigable Small World*): un índice que encuentra los vectores
más cercanos sin compararlos con todos. Es **aproximado**: a cambio de muchísima
velocidad, puede no devolver exactamente el más cercano. Con 37 fragmentos da igual;
con millones, es lo que hace viable la búsqueda.

El tamaño de la columna (768) está **atado al modelo**. Si se cambia de modelo de
embeddings, hay que cambiar la columna y volver a procesar todos los documentos: los
vectores de modelos distintos no son comparables.

### Trabajo en segundo plano: `Channel` + `BackgroundService`

Generar embeddings con Ollama en CPU tarda (unos segundos por documento, mucho más
con un PDF grande). La subida no puede esperar a eso:

```csharp
// Endpoint de subida: encola y responde.
ingestionQueue.Enqueue(document.Id);

// Worker: lee de la cola de uno en uno.
await foreach (var documentId in queue.ReadAllAsync(stoppingToken))
{
    using var scope = scopeFactory.CreateScope();   // un DbContext por documento
    await scope.ServiceProvider.GetRequiredService<DocumentIngestionService>()
        .IngestAsync(documentId, stoppingToken);
}
```

| .NET | Spring |
|---|---|
| `BackgroundService` + `AddHostedService` | Un `@Component` con un bucle en un hilo, o `@Async` |
| `Channel<int>` | `BlockingQueue<Integer>` |
| `IServiceScopeFactory.CreateScope()` | Abrir una transacción/sesión nueva fuera de la petición |

- **Por qué un scope**: el worker es singleton y vive toda la aplicación, pero el
  `DbContext` es scoped (uno por petición). El worker crea su propio scope por documento.
- **La cola está en memoria**: si la aplicación se para, se pierde. No importa,
  porque el estado está en la base de datos: al arrancar, el worker busca los
  documentos `Pending` o `Processing` y los vuelve a encolar.
- **En producción** se usaría una cola duradera (Azure Service Bus, RabbitMQ) o
  Hangfire, y se podrían procesar varios documentos en paralelo.

### `202 Accepted` y *polling*

`POST /ingest` responde **202 Accepted**: "lo he recibido, pero todavía no está
hecho". El cliente consulta el estado en la URL del documento. El frontend lo hace
solo mientras haya documentos sin terminar:

```ts
private scheduleRefresh(): void {
  clearTimeout(this.refreshTimer);
  if (this.documents().some((d) => isInProgress(d.status))) {
    this.refreshTimer = setTimeout(() => this.load(), refreshIntervalMs);
  }
}
```

Alternativas para avisar sin preguntar: *Server-Sent Events* o SignalR (WebSockets).

### Transacción para reemplazar los fragmentos

Reprocesar un documento borra sus fragmentos y guarda los nuevos. Si fallara a mitad,
quedaría sin fragmentos. Por eso va en una transacción:

```csharp
await using var transaction = await db.Database.BeginTransactionAsync(ct);
await db.DocumentChunks.Where(c => c.DocumentId == id).ExecuteDeleteAsync(ct);
db.DocumentChunks.AddRange(nuevos);
await db.SaveChangesAsync(ct);
await transaction.CommitAsync(ct);
```

*Es el `@Transactional` de Spring, pero explícito.* Los embeddings se generan **antes**
de abrir la transacción, para no tenerla abierta mientras se espera a Ollama.

### Tests sin modelo real

Los tests de integración sustituyen Ollama por `FakeEmbeddingGenerator`, que devuelve
vectores de 768 números calculados a partir del texto: sin significado, pero
deterministas y en milisegundos. Así se prueba todo el flujo (cola, worker, pgvector,
estados) sin depender de un modelo. Con un texto marcado, el falso falla, para probar
el estado `Failed`.

```csharp
builder.ConfigureTestServices(services =>
{
    services.RemoveAll<IEmbeddingGenerator<string, Embedding<float>>>();
    services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>, FakeEmbeddingGenerator>();
});
```

*Es un `@MockBean` / `@TestConfiguration` de Spring.*

## Archivos importantes

```
backend/src/DocAssist.Api/
├── appsettings.json                     ← secciones Ingestion y Ollama
├── Program.cs                           ← UseVector, AddEmbeddingGenerator, cola y worker
├── Domain/
│   ├── DocumentChunk.cs                 ← fragmento + Vector, EmbeddingDimensions = 768
│   ├── DocumentStatus.cs
│   └── Document.cs                      ← + Status, StatusMessage, Chunks
├── Data/Configurations/DocumentChunkConfiguration.cs ← vector(768), HNSW, cascada
├── Data/Migrations/..._AddDocumentChunks.cs           ← default "Pending" corregido a mano
└── Features/Documents/
    ├── DocumentEndpoints.cs             ← + /ingest y /chunks; la subida encola
    └── Ingestion/
        ├── TextChunker.cs               ← troceado con solapamiento (código puro)
        ├── DocumentTextExtractor.cs     ← Markdown y PDF (PdfPig)
        ├── DocumentIngestionService.cs  ← texto → fragmentos → embeddings → BD
        ├── DocumentIngestionQueue.cs    ← Channel en memoria
        ├── DocumentIngestionWorker.cs   ← BackgroundService
        └── IngestionOptions.cs          ← IngestionOptions y OllamaOptions

backend/tests/DocAssist.Api.Tests/
├── Infrastructure/FakeEmbeddingGenerator.cs
└── Features/Documents/Ingestion/
    ├── TextChunkerTests.cs              ← 16 tests unitarios
    ├── DocumentTextExtractorTests.cs    ← 3 (el PDF se genera con PdfPig en el test)
    └── DocumentIngestionTests.cs        ← 6 de integración

frontend/src/app/features/documents/
├── uploaded-document.ts                 ← DocumentStatus, statusLabels, isInProgress
├── document-service.ts                  ← + ingest()
└── document-list/                       ← columna Estado, Reprocesar y polling
```

Paquetes nuevos: `Pgvector.EntityFrameworkCore`, `Microsoft.Extensions.AI`,
`OllamaSharp` y `PdfPig`.

## Cómo probarlo

```powershell
# 1. Base de datos y Ollama (desde la raíz)
docker compose up -d db ollama

# 2. API y Angular
dotnet run --project backend/src/DocAssist.Api
cd frontend; npm start
```

1. En `http://localhost:4200/documents`, los 13 documentos de ejemplo salen como
   **Listo**: "13 documentos, 13 listos para el asistente".
2. Sube un documento nuevo: aparece como **Pendiente**, pasa a **Procesando** y a los
   pocos segundos a **Listo**, sin recargar la página.
3. Pulsa **Reprocesar** en cualquiera: vuelve a pasar por los mismos estados.
4. Para ver un **Error**, para Ollama (`docker compose stop ollama`) y reprocesa un
   documento: sale el motivo debajo de la etiqueta. Vuelve a arrancarlo y reprocesa.
5. Fragmentos de un documento: `http://localhost:5080/api/documents/14/chunks`.
6. Los vectores en la base de datos:

   ```powershell
   docker exec -it docassist-db psql -U docassist -d docassist `
     -c "select document_id, index, length(content), vector_dims(embedding) from document_chunks limit 5;"
   ```

**Adelanto del paso 9**, buscando a mano con SQL (pregunta convertida en vector con
Ollama y ordenada por distancia coseno), los fragmentos más parecidos salen del
documento correcto:

| Pregunta | Fragmento más parecido | Similitud |
|---|---|---|
| ¿La Xbox Series S tiene lector de discos? | `manual-microsoft-xbox-series-s.md` | 0,785 |
| ¿Cuántos días tengo para devolver un producto? | `devoluciones.md` | 0,738 |
| ¿Puedo ampliar la memoria RAM del portátil gaming? | `manual-asus-rog-strix-g16.md` | 0,715 |

Tests:

```powershell
dotnet test backend/DocAssist.slnx      # 66 (necesita Docker, no necesita Ollama)
cd frontend; npx ng test --watch=false  # 32
```

## Decisiones y alternativas

| Decisión | Por qué | Alternativas |
|---|---|---|
| Troceador propio | Código pequeño, se entiende y se prueba; respeta párrafos | `TextChunker` de Semantic Kernel o `Microsoft.Extensions.DataIngestion`; trocear por tokens con un *tokenizer* real |
| ~1.000 caracteres y 200 de solapamiento | Punto de partida habitual; configurable sin tocar código | Trocear por secciones de Markdown (`##`); fragmentos más pequeños para preguntas muy concretas |
| Markdown sin limpiar | El modelo entiende el formato y la cita se lee mejor | Quitar `#`, `|` y `*` antes de generar el vector |
| Prefijo `search_document: ` y cabecera con documento y producto | Mejor búsqueda con `nomic-embed-text` y contexto en cada fragmento | Guardar el producto en una columna y filtrar por él en el paso 9 |
| Ingesta en segundo plano con `Channel` | La subida responde al momento; Ollama en CPU es lento | Ingesta síncrona en la petición (más simple, pero bloquea). Hangfire o una cola duradera en producción |
| Un documento a la vez | Ollama en CPU no gana nada con varios en paralelo | Varios consumidores de la cola |
| Ingesta automática al subir + endpoint para reprocesar | Lo normal es querer el documento listo; reprocesar sirve tras un fallo o al cambiar el troceado | Solo ingesta manual |
| Polling cada 2 s solo mientras hay trabajo | Simple y sin infraestructura extra | Server-Sent Events o SignalR |
| Índice HNSW | Rápido también con muchos datos y sin reentrenar | IVFFlat (hay que crearlo con datos ya cargados); sin índice (búsqueda exacta, vale para pocos datos) |
| `vector(768)` fijo | Lo exige pgvector para indexar | Columna sin tamaño, que no admite índice |
| Fragmentos con `ON DELETE CASCADE` | No tienen sentido sin su documento | Borrarlos a mano en el endpoint |
| `FakeEmbeddingGenerator` en los tests | Tests rápidos y deterministas, sin Ollama | Testcontainers con Ollama (lento, descarga GB) |

## Para la entrevista

**Frases que puedo decir:**

> "Al subir un documento, la API responde al momento y lo encola. Un `BackgroundService`
> extrae el texto, lo trocea en fragmentos de unos mil caracteres con solapamiento,
> genera los embeddings con Ollama y los guarda en Postgres con pgvector, con un índice
> HNSW por distancia coseno."

> "Uso las abstracciones de `Microsoft.Extensions.AI`: el código depende de
> `IEmbeddingGenerator`, y Ollama solo aparece en el registro de `Program.cs`. Cambiar a
> Azure OpenAI es cambiar esa línea."

> "El troceador es código puro y está cubierto con tests unitarios: que ningún fragmento
> pase del tamaño máximo, que haya solapamiento, que no se pierda ni se corte ninguna
> palabra. Es la parte del RAG que se puede probar sin IA."

**Posibles preguntas:**

- *¿Qué es un embedding?*
  Un vector de números que representa el significado de un texto. Textos parecidos en
  significado dan vectores cercanos, y eso permite buscar por significado y no por
  palabras.

- *¿Por qué trocear los documentos?*
  Porque un vector de un documento entero mezcla todos sus temas y no se parece a
  ninguna pregunta concreta, y porque al LLM solo hay que pasarle lo relevante.

- *¿Para qué sirve el solapamiento?*
  Para que una idea que cae en la frontera entre dos fragmentos no quede partida.

- *¿Qué pasa si cambias de modelo de embeddings?*
  Los vectores de modelos distintos no son comparables y pueden tener otro tamaño. Hay
  que cambiar la columna y volver a procesar todos los documentos.

- *¿Qué es HNSW?*
  Un índice para búsqueda aproximada de vecinos cercanos. Organiza los vectores en un
  grafo por capas y encuentra los más parecidos sin compararlos con todos.

- *¿Qué pasa si la aplicación se cae a mitad de una ingesta?*
  La cola está en memoria y se pierde, pero el estado está en la base de datos. Al
  arrancar, el worker vuelve a encolar los documentos pendientes o a medias. Y el
  reemplazo de fragmentos va en una transacción, así que nunca quedan a medias.

- *¿Cómo pruebas algo que depende de un modelo de IA?*
  Separo lo determinista (troceado, extracción) y lo pruebo con tests unitarios. Para el
  flujo completo sustituyo el modelo por un generador falso que devuelve vectores
  deterministas. La calidad de la búsqueda se evalúa aparte, con preguntas de prueba.
