# 00 · Plan y progreso

Lista de los 12 pasos del proyecto con una descripción breve de cada uno, para ir
marcando lo que se va completando.

Un paso se considera **terminado** cuando cumple las cuatro cosas: el código
funciona y se ha probado, existe su documento en `/docs`, hay un commit con
mensaje en formato Conventional Commits, y está subido a GitHub.

## Progreso

**5 de 12 pasos completados**

`████████████░░░░░░░░░░░░░░░░░░` 42 %

---

## Requisitos previos

- [x] **.NET 10 SDK** — versión instalada: `10.0.401`
- [x] **Node.js 20+** — versión instalada: `v20.11.1`
- [x] **Docker Desktop** — versión instalada: `29.8.0`
- [x] **Git** — versión instalada: `2.49.0`
- [x] **Modelos de Ollama descargados** — `nomic-embed-text` (768 dimensiones) y `llama3.1:8b` (tool calling verificado)
- [ ] **Docker en el PATH del terminal** — ver nota al final

---

## Pasos

### [x] 1 — Entorno y estructura del repositorio

Andamiaje del proyecto: repositorio git, carpetas `backend/` `frontend/` `docs/`
`infra/`, `.gitignore`, `.editorconfig`, `.gitattributes`, `docker-compose.yml`
con PostgreSQL+pgvector y Ollama, `.env.example`, README con diagrama de
arquitectura y `AI_REVIEW.md`.

> 📄 [`01-entorno-y-estructura.md`](01-entorno-y-estructura.md) · commit `025e3f6`

---

### [x] 2 — Primer proyecto .NET: solución y Minimal API

Crear la solución `.slnx`, el proyecto de API y el de tests. Entender `Program.cs`,
el `WebApplicationBuilder`, `appsettings.json`, un endpoint `/health` y la
documentación automática de la API con OpenAPI.

**Conceptos nuevos:** Minimal APIs, `WebApplicationBuilder`, configuración por
entornos. *Equivalente a `@SpringBootApplication` + `application.yml` + Swagger.*

> 📄 [`02-solucion-dotnet-y-minimal-api.md`](02-solucion-dotnet-y-minimal-api.md) · 3 tests de integración en verde

> 🔀 **Cambio de dominio entre los pasos 2 y 3:** la aplicación pasa de ser un
> asistente de documentación técnica a un **asistente de tienda de tecnología**
> (móviles, ordenadores y consolas). La arquitectura y el stack no cambian.
> Explicado en [`02b-cambio-de-dominio.md`](02b-cambio-de-dominio.md).

---

### [x] 3 — EF Core, PostgreSQL y migraciones

Entidad `Product` (nombre, marca, modelo, categoría, precio, stock, fecha de
lanzamiento y especificaciones opcionales: RAM, almacenamiento y pantalla),
`AppDbContext`, configuración con Fluent API, cadena de conexión desde
configuración, primera migración y aplicarla contra la base de datos del
contenedor. Datos de ejemplo: 10 productos (4 móviles, 3 ordenadores, 3
consolas) con `UseSeeding`, solo en desarrollo. *Health checks* separados en
*liveness* (`/health`) y *readiness* (`/health/ready`).

**Conceptos nuevos:** `DbContext`, `DbSet`, migraciones de EF Core, `dotnet ef`.
*Equivalente a JPA `@Entity` + `JpaRepository` + Flyway.*

> 📄 [`03-ef-core-y-migraciones.md`](03-ef-core-y-migraciones.md) · migración `InitialCreate` aplicada · 10 productos

---

### [x] 4 — CRUD de productos: endpoints, validación y errores

Endpoints agrupados con `MapGroup("/api/products")` en su propio archivo, DTOs
con `record`, validación de entrada, manejo global de errores devolviendo
ProblemDetails (RFC 9457), logging estructurado. Tests unitarios de validación y
tests de integración de los endpoints contra Postgres con Testcontainers.

**Conceptos nuevos:** `MapGroup`, `record`, `TypedResults`, validación de
.NET 10, ProblemDetails, middleware, logging estructurado, Testcontainers.
*Equivalente a `@RestController` + `@Valid` + `@ControllerAdvice` +
`@SpringBootTest` + `@Testcontainers`.*

- [x] 1. DTOs de entrada y salida
- [x] 2. Endpoints de lectura (`GET`)
- [x] 3. Endpoints de escritura (`POST`, `PUT`, `DELETE`)
- [x] 4. Validación de entrada
- [x] 5. Errores centralizados con ProblemDetails
- [x] 6. Logging estructurado
- [x] 7. Tests unitarios de la validación (12)
- [x] 8. Tests de integración con Testcontainers (6)
- [x] 9. Cierre: documentación y commit final

> 📄 [`04-crud-de-productos.md`](04-crud-de-productos.md) · 21 tests en verde (12 unitarios + 6 de integración + 3 del paso 2)

---

### [x] 5 — Frontend Angular: proyecto, routing y lista de productos

Crear la app Angular con standalone components, configurar `provideHttpClient` y
el routing, un `ProductService` inyectable, signals para el estado, y una tabla
que consuma la API. Proxy de desarrollo para evitar problemas de CORS.

**Conceptos nuevos:** standalone components, signals, inyección de dependencias
de Angular, `HttpClient`, `@if`/`@for`. *Equivalente a Vite + React Router +
un hook `useProducts`; signals frente a `useState`.*

- [x] 1. Arrancar `ng serve` en la carpeta renombrada
- [x] 2. Layout mínimo (cabecera + `router-outlet`)
- [x] 3. Proxy de desarrollo `/api` → `localhost:5080`
- [x] 4. Modelo `Product` y `provideHttpClient()`
- [x] 5. `ProductService`
- [x] 6. Componente `ProductList` con signals, `@if`/`@for` y ruta *lazy*
- [x] 7. Test del `ProductService`
- [x] 8. Cierre: documentación y commit final

> 📄 [`05-frontend-angular-y-lista.md`](05-frontend-angular-y-lista.md) · 4 tests en verde (Vitest)

---

### [ ] 6 — Formularios reactivos: crear y editar productos

Formulario reactivo con validación y mensajes de error, estados de carga, borrado
con confirmación. Un test de componente con `TestBed`.

**Conceptos nuevos:** `FormGroup`, `FormControl`, `Validators`, `TestBed`.
*Equivalente a react-hook-form + Zod.*

---

### [ ] 7 — Subida de documentos

Entidad `Document`, endpoint que acepta ficheros multipart, límites de tamaño y
tipos permitidos (`.md` y `.pdf`), almacenamiento en disco con ruta
configurable, y la interfaz de subida en Angular.

> **Nota para este paso:** la relación de `Document` con `Product` será
> **opcional**. Un manual pertenece a un producto concreto; los documentos
> generales de la tienda (garantía, devoluciones, envíos) no pertenecen a
> ninguno y tendrán la clave foránea a nulo.

**Conceptos nuevos:** `IFormFile`, `multipart/form-data`, relaciones uno a muchos
en EF Core. *Equivalente a `MultipartFile` en Spring.*

---

### [ ] 8 — Ingesta: troceado, embeddings y pgvector

Extraer el texto de Markdown y PDF, trocearlo en *chunks* con solapamiento,
generar los embeddings con `IEmbeddingGenerator` apuntando a Ollama, y guardarlos
en una tabla `DocumentChunk` con columna de tipo `vector` e índice HNSW. Endpoint
de ingesta y estado del documento (pendiente / procesando / listo). Tests del
troceador, que es código puro sin IA.

**Conceptos nuevos:** chunking y solapamiento, embeddings, `Microsoft.Extensions.AI`,
índices vectoriales. *El corazón del RAG.*

---

### [ ] 9 — Búsqueda semántica (todavía sin LLM)

Endpoint `POST /api/search` que genera el embedding de la pregunta y devuelve los
`k` fragmentos más parecidos por distancia coseno, con su puntuación de
similitud.

**Por qué este paso separado:** aislar la recuperación antes de meter el LLM es lo
que hace que el RAG se pueda depurar. Si las respuestas salen mal, aquí se ve si
el problema es la búsqueda o el *prompt*.

---

### [ ] 10 — Chat con RAG y citas

`IChatClient` con un *prompt* de sistema bien diseñado: responder solo con el
contexto recuperado, admitir cuando no se sabe la respuesta, y citar las fuentes.
La respuesta incluye la lista de citas (documento y fragmento). Interfaz de chat
en Angular con las citas visibles.

**Conceptos nuevos:** ingeniería de *prompt*, *grounding*, citas verificables,
streaming de respuestas.

---

### [ ] 11 — Agente con tool calling

Convertir el chat en un agente con dos herramientas: `search_documentation` (busca
en los manuales y las políticas de la tienda) y `query_products` (consulta la
base de datos de productos, por ejemplo "¿qué móviles tenemos por debajo de
500 € con al menos 8 GB de RAM?"). Bucle de invocación de herramientas y traza
en la interfaz de qué herramientas se han llamado.

**Conceptos nuevos:** *tool calling* / *function calling*, descripción de
herramientas, el bucle del agente. *Es la parte de más valor para la entrevista.*

---

### [ ] 12 — Docker Compose completo, CI y pulido final

Dockerfiles multi-stage para backend y frontend, `docker-compose.yml` con los
cuatro servicios y sus *healthchecks*, workflow de GitHub Actions que compile y
ejecute los tests de las dos mitades, README final revisado e índice de `/docs`
completo.

**Conceptos nuevos:** builds multi-stage, `dotnet publish`, GitHub Actions con
`setup-dotnet` y `setup-node`. *Equivalente a lo que ya conoces de Docker y
GitHub Actions con Maven.*

---

## Hallazgos de la verificación que afectan a pasos futuros

Pruebas hechas contra los modelos reales al cerrar el paso 1:

| Hallazgo | Dato medido | Afecta a |
|---|---|---|
| Los embeddings de `nomic-embed-text` tienen **768 dimensiones** | `/api/embed` devuelve un vector de 768 | Paso 8: la columna será `vector(768)`. Si se cambia de modelo de embeddings, hay que cambiar la columna y regenerar todos los vectores |
| `llama3.1:8b` corre **100 % en CPU**, a unos **6 tokens/s** | `ollama ps` + segunda llamada en caliente | Pasos 10 y 11: una respuesta de 200 tokens tarda ~35 s. Conviene hacer *streaming* para que el usuario vea el texto aparecer |
| La primera llamada tarda **~49 s** | carga del modelo en memoria | Demo: lanzar una pregunta de calentamiento antes de enseñarlo |
| La GPU es una **GTX 1650 de 4 GB** | `nvidia-smi` | `llama3.1:8b` (4,9 GB) no cabe entero. Opción a valorar en el paso 10: un modelo de 3B con tool calling (`llama3.2:3b`, `qwen2.5:3b`) que sí cabe en la GPU |
| El contexto por defecto es de **4096 tokens** | `ollama ps` | Paso 10: con varios *chunks* de contexto puede quedarse corto; habrá que ajustar `num_ctx` |
| En la prueba de tool calling, el modelo envió un argumento numérico como **texto** (`"100"` en lugar de `100`) | prueba de tool calling | Paso 11: los argumentos de las herramientas hay que validarlos y convertirlos, nunca fiarse del tipo que manda el modelo |

## Nota: carpeta renombrada (paso 5)

La carpeta del proyecto se llamaba `proyecto-C#-Angular`. El `#` impedía arrancar
Angular, porque Vite convierte las rutas en URLs y en una URL el `#` corta la ruta.
Se renombró a **`C:\dev\PERSONAL\proyecto-dotnet-angular`**.

Para no perder datos, `docker-compose.yml` fija `name: proyecto-c-angular`: así
Compose sigue usando los volúmenes originales (`proyecto-c-angular_db-data` y
`proyecto-c-angular_ollama-data`) aunque la carpeta tenga otro nombre. Detalle en la
entrada 08 de `AI_REVIEW.md`.

También se actualizó Node a **24 LTS** (24.21.0, con nvm-windows), porque Angular 22
exige Node 22.22+ o 24.15+.

## Nota: Docker en el PATH

Docker Desktop se ha instalado en una ruta de usuario
(`%LOCALAPPDATA%\Programs\DockerDesktop`) y su carpeta `resources\bin` no estaba
en el `PATH` del terminal. Si al escribir `docker --version` sale "command not
found", hay dos opciones:

1. **Cerrar y volver a abrir el terminal.** Docker Desktop añade la carpeta al
   `PATH` del usuario al completar la instalación, pero los terminales ya abiertos
   siguen con el `PATH` antiguo.
2. **Añadirlo a mano** en la sesión actual:

   ```powershell
   $env:PATH = "$env:LOCALAPPDATA\Programs\DockerDesktop\resources\bin;$env:PATH"
   ```

Para que sea permanente, en Docker Desktop: *Settings → Advanced → Allow the
default Docker socket / Add CLI tools to PATH*.
