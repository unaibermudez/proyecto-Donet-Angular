# 09 · Búsqueda semántica (todavía sin LLM)

## Qué hemos hecho

La "R" del RAG (*retrieval*, recuperación): dada una pregunta, encontrar los fragmentos
de los documentos que hablan de lo mismo, aunque no usen las mismas palabras.

```
"¿La Xbox Series S tiene lector de discos?"
        │ 1. "search_query: " + pregunta → Ollama (nomic-embed-text) → vector de 768
        ▼
Postgres + pgvector
        │ 2. ORDER BY embedding <=> @vector LIMIT 5   (índice HNSW)
        ▼
Fragmentos ordenados por similitud: manual-microsoft-xbox-series-s.md (0,79), ...
```

**Backend:**

- **`POST /api/search`** con `question` (3-500 caracteres), `topK` opcional (1-20, por
  defecto 5) y `productId` opcional para buscar solo en los documentos de un producto.
  Devuelve los fragmentos con su documento, producto, posición, texto y
  **similitud** (0 a 1), y cuánto tardó la búsqueda.
- **`SemanticSearchService`**: la lógica, separada del endpoint, porque la usarán
  también el chat (paso 10) y el agente (paso 11).
- **Prefijo `search_query: `** para las preguntas, la pareja del `search_document: `
  de la ingesta. Configurable en `Ollama:EmbeddingQueryPrefix`.

**Frontend**: página **Buscar** (`/search`) con la pregunta, el filtro por producto,
el número de resultados, preguntas de ejemplo y los fragmentos con una barra de similitud.

**Tests**: 73 en el backend (+7) y 37 en el frontend (+5).

## Conceptos nuevos

### Por qué un paso sin LLM

Un RAG tiene dos partes que pueden fallar: **encontrar** la información y
**redactar** la respuesta. Si el chat responde mal, hay que saber cuál falló:

- Si la búsqueda no trae el fragmento correcto, ningún *prompt* lo arregla.
- Si lo trae y la respuesta sigue mal, el problema es el *prompt* o el modelo.

Esta página permite ver exactamente qué leerá el LLM antes de responder.

### La consulta con pgvector en EF Core

```csharp
var rows = await db.DocumentChunks
    .OrderBy(c => c.Embedding.CosineDistance(questionVector))
    .Take(topK)
    .Select(c => new { c.Content, c.Document.FileName, Distance = c.Embedding.CosineDistance(questionVector) })
    .ToListAsync();
```

EF Core lo traduce a SQL (comprobado en el log):

```sql
SELECT ... , d.embedding <=> @questionVector AS "Distance"
FROM document_chunks AS d
INNER JOIN documents AS d0 ON d.document_id = d0.id
LEFT JOIN products AS p ON d0.product_id = p.id
ORDER BY d.embedding <=> @questionVector
LIMIT @p
```

- **`<=>`** es la distancia coseno de pgvector. `<->` sería la distancia euclídea (L2)
  y `<#>` el producto escalar negativo.
- **Similitud = 1 − distancia**. Se devuelve la similitud porque se entiende mejor:
  más alto = más parecido.
- **`ORDER BY ... LIMIT`** es el patrón que acelera el índice HNSW. Con un `WHERE`
  (filtro por producto), Postgres puede decidir no usar el índice; con pocos datos da
  igual, con millones habría que vigilarlo (pgvector 0.8 mejora este caso con
  *iterative index scans*).

### Qué significa la similitud

Con `nomic-embed-text` y estos documentos (medido con Ollama real):

| Pregunta | 1.º resultado | Similitud |
|---|---|---|
| ¿Cuántos días tengo para devolver un producto? | `devoluciones.md` | 0,80 |
| ¿La Xbox Series S tiene lector de discos? | `manual-microsoft-xbox-series-s.md` | 0,79 |
| ¿Se puede ampliar la memoria RAM del portátil gaming? | `manual-hp-victus-15l.md` (y el ASUS en 3.er lugar) | 0,71 |
| ¿Qué hago si el joystick del mando se mueve solo? | `manual-nintendo-switch-2.md` (*drift*) | 0,66 |
| ¿Qué móvil admite tarjeta microSD? | `manual-samsung-galaxy-s25-ultra.md` | 0,62 |

La similitud **no es un porcentaje de acierto**: depende del modelo y del texto. Sirve
para comparar resultados de la misma búsqueda, no como umbral universal. Por eso no se
filtra por similitud mínima todavía: primero hay que ver valores reales.

### Las limitaciones que se ven aquí

La última pregunta es el ejemplo perfecto. El primer resultado es el Samsung, cuyo
manual dice que **no** admite microSD. El Xiaomi, que **sí** la admite, no sale entre
los tres primeros. Los embeddings capturan **de qué habla** un texto, no si afirma o
niega. Mejoras posibles, de menos a más trabajo:

1. **Pedir más fragmentos** (`topK`) y dejar que el LLM lea y decida (paso 10).
2. **Búsqueda híbrida**: combinar la semántica con búsqueda por palabras (*full-text
   search* de Postgres) y mezclar los rankings (*Reciprocal Rank Fusion*).
3. **Re-ranking**: un segundo modelo (*cross-encoder*) reordena los 20 primeros leyendo
   pregunta y fragmento juntos.
4. **Datos estructurados**: "¿qué móvil admite microSD?" es una pregunta de catálogo;
   el agente del paso 11 podrá consultar la base de datos de productos.

### Tests de búsqueda sin modelo real

El `FakeEmbeddingGenerator` del paso 8 devolvía vectores aleatorios, que no sirven para
probar el **orden** de los resultados. Ahora usa **bolsa de palabras**: cada palabra
suma 1 en una posición fija del vector (según su hash). Dos textos que comparten
palabras dan vectores cercanos. No entiende sinónimos, pero es determinista y basta para
comprobar que el fragmento que más se parece sale primero, el `topK`, el orden por
similitud y el filtro por producto.

Para no depender de lo que suban otros tests, cada uno usa palabras inventadas propias
("zarzamora", "kumquat", "pitahaya").

## Archivos importantes

```
backend/src/DocAssist.Api/
├── appsettings.json                      ← Ollama:EmbeddingQueryPrefix
├── Program.cs                            ← SemanticSearchService y MapSearchEndpoints
└── Features/Search/
    ├── SearchRequest.cs                  ← pregunta, topK, productId (con validación)
    ├── SearchResponse.cs                 ← SearchResponse y SearchResult
    ├── SemanticSearchService.cs          ← embedding de la pregunta + consulta pgvector
    └── SearchEndpoints.cs                ← POST /api/search

backend/tests/DocAssist.Api.Tests/
├── Infrastructure/FakeEmbeddingGenerator.cs  ← ahora bolsa de palabras
└── Features/Search/SearchEndpointsTests.cs   ← 7 tests de integración

frontend/src/app/features/search/
├── search.ts                             ← interfaces
├── search-service.ts
└── search-page/                          ← página /search
```

## Cómo probarlo

```powershell
docker compose up -d db ollama
dotnet run --project backend/src/DocAssist.Api
cd frontend; npm start
```

1. En `http://localhost:4200/search`, pulsa una pregunta de ejemplo.
2. Prueba las de la tabla de arriba y compara las similitudes.
3. Busca "¿Cómo se carga?" en **Todos los documentos** y después solo en **Nintendo
   Switch 2**: con el filtro, solo salen fragmentos de su manual.
4. Busca algo que no esté en ningún documento ("¿Hacéis envíos a Marte?"): siempre
   devuelve resultados, pero con similitudes más bajas. La búsqueda no sabe decir "no
   lo sé"; eso será trabajo del *prompt* en el paso 10.
5. Desde Scalar (`http://localhost:5080/scalar`) o PowerShell:

   ```powershell
   Invoke-RestMethod http://localhost:5080/api/search -Method Post `
     -ContentType 'application/json; charset=utf-8' `
     -Body '{"question":"¿Cuánto dura la garantía?","topK":3}'
   ```

Tests:

```powershell
dotnet test backend/DocAssist.slnx      # 73 (necesita Docker, no Ollama)
cd frontend; npx ng test --watch=false  # 37
```

## Decisiones y alternativas

| Decisión | Por qué | Alternativas |
|---|---|---|
| `POST` para buscar | La pregunta va en el cuerpo: puede ser larga y con cualquier carácter | `GET /api/search?q=...` (cacheable, pero con límites de longitud y codificación) |
| Servicio separado del endpoint | Lo reutilizarán el chat y el agente | Lógica dentro del endpoint |
| Distancia coseno | Es la que se usa con estos modelos y la del índice | Producto escalar (equivalente con vectores normalizados), L2 |
| Devolver la similitud y no la distancia | Se entiende mejor | Devolver la distancia tal cual |
| Sin similitud mínima | Primero hay que ver valores reales; el umbral depende del modelo | Descartar resultados por debajo de 0,5, por ejemplo |
| `topK` por defecto 5, máximo 20 | Suficiente contexto para el LLM sin llenar su ventana (4.096 tokens por defecto en Ollama) | Más fragmentos y re-ranking |
| Filtro por producto que excluye los generales | Simple y predecible | Incluir también los documentos generales (útil para "garantía de la PS5") |
| Bolsa de palabras en el generador falso | Permite probar el orden sin modelo real | Vectores aleatorios (no sirven para el orden); Ollama en los tests (lento) |
| Preguntas de ejemplo en la página | Probar rápido en una demo | Página vacía |

## Para la entrevista

**Frases que puedo decir:**

> "Antes de meter el LLM, separé la recuperación en su propio endpoint. Así, si el chat
> responde mal, puedo ver si el problema es que la búsqueda no trajo el fragmento o que
> el modelo no lo usó bien."

> "La búsqueda convierte la pregunta en un vector con el mismo modelo que los documentos
> y ordena los fragmentos por distancia coseno con pgvector. EF Core traduce
> `CosineDistance` al operador `<=>`, y el `ORDER BY ... LIMIT` usa el índice HNSW."

> "Vi una limitación clara: con '¿qué móvil admite microSD?' salía primero el manual que
> dice que no la admite. Los embeddings captan el tema, no la negación. Lo mitigaría con
> búsqueda híbrida y re-ranking, y en el agente con una herramienta que consulte el
> catálogo."

**Posibles preguntas:**

- *¿Qué diferencia hay entre búsqueda semántica y por palabras clave?*
  La de palabras clave busca coincidencias exactas (o con raíces); la semántica compara
  significados, así que encuentra "devolver" aunque el texto diga "reembolso". La de
  palabras clave es mejor con nombres propios, códigos y números exactos. Lo ideal es
  combinarlas (búsqueda híbrida).

- *¿Por qué hay que usar el mismo modelo para los documentos y para la pregunta?*
  Porque cada modelo tiene su propio "espacio" de vectores. Un vector de un modelo no se
  puede comparar con uno de otro.

- *¿Cómo sabes si la búsqueda funciona bien?*
  Con un conjunto de preguntas de prueba y el documento que debería salir, y midiendo
  cuántas veces aparece en los k primeros (*recall@k*). Esta página es la versión manual
  de esa evaluación.

- *¿Qué pasa si la pregunta no tiene respuesta en los documentos?*
  La búsqueda siempre devuelve los k más parecidos, aunque se parezcan poco. Decidir que
  no hay respuesta es trabajo del LLM con un buen *prompt*, o de un umbral de similitud.
