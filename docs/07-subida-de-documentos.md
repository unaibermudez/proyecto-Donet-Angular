# 07 · Subida de documentos

## Qué hemos hecho

La tienda ya puede guardar los documentos que serán la base de conocimiento del
asistente: manuales de productos y políticas generales (garantía, devoluciones, envíos).

**Backend**, en `/api/documents`:

| Método | Ruta | Qué hace | Respuestas |
|---|---|---|---|
| `GET` | `/api/documents` | Lista los documentos, los más recientes primero. Filtro opcional `?productId=` | 200 |
| `GET` | `/api/documents/{id}` | Datos de un documento | 200, 404 |
| `GET` | `/api/documents/{id}/content` | Descarga el fichero con su nombre original | 200, 404 |
| `POST` | `/api/documents` | Sube un fichero como `multipart/form-data` (campos `file` y `productId` opcional) | 201 + `Location`, 400, 413 |
| `DELETE` | `/api/documents/{id}` | Borra la fila y el fichero del disco | 204, 404 |

- **Entidad `Document`** con relación **uno a muchos opcional** con `Product`. Si se
  borra un producto, sus documentos se quedan como generales (`ON DELETE SET NULL`).
- **Migración `AddDocuments`**, ya aplicada a la base de datos de desarrollo.
- **Reglas de los ficheros**: solo `.md` y `.pdf`, como mucho 10 MB, no vacíos, y se
  comprueba el **contenido** además de la extensión: un PDF tiene que empezar por
  `%PDF-` y un Markdown no puede tener bytes a cero.
- **Almacenamiento en disco** detrás de una interfaz (`IDocumentStorage`), en la
  carpeta configurada en `DocumentStorage:RootPath`. Cada fichero se guarda con un
  nombre nuevo (GUID); el nombre original solo se guarda en la base de datos.
- **Configuración validada al arrancar** (`ValidateOnStart`).

**Frontend**, en `/documents` (nuevo enlace **Documentos** en la cabecera):

- **Formulario de subida** (`DocumentUpload`): fichero y producto opcional, con
  comprobación previa en el navegador y errores de la API.
- **Lista** (`DocumentList`): nombre (enlace de descarga), producto o "General",
  tamaño legible (pipe propio `fileSize`), fecha de subida y botón de borrar.

**Documentos de ejemplo** en `infra/sample-documents/`: garantía, devoluciones,
envíos y el manual de la PlayStation 5 Slim. Son inventados y servirán para la
ingesta del paso 8.

**Tests**: 41 en el backend (+13 unitarios de las reglas y +7 de integración) y 29 en
el frontend (+14).

## Conceptos nuevos

### `multipart/form-data`

Un JSON no puede llevar un fichero binario. Un formulario `multipart/form-data` va en
**partes** separadas por una línea frontera (el *boundary*), cada una con sus cabeceras:

```
POST /api/documents
Content-Type: multipart/form-data; boundary=----abc123

------abc123
Content-Disposition: form-data; name="file"; filename="garantia.md"
Content-Type: text/markdown

# Garantía ...
------abc123
Content-Disposition: form-data; name="productId"

8
------abc123--
```

En Angular se construye con `FormData`. **No hay que poner la cabecera `Content-Type`
a mano**: el navegador la añade con el *boundary*. Si la pones tú, falta el *boundary*
y el servidor no sabe separar las partes.

```ts
const form = new FormData();
form.append('file', file);
form.append('productId', String(productId));
this.http.post<UploadedDocument>('/api/documents', form);
```

### `IFormFile` en una Minimal API

```csharp
private static async Task<...> UploadDocument(
    IFormFile? file,             // la parte "file" del formulario
    [FromForm] int? productId,   // la parte "productId"
    ...)
```

| .NET | Spring |
|---|---|
| `IFormFile` | `MultipartFile` |
| `file.FileName`, `file.Length` | `getOriginalFilename()`, `getSize()` |
| `file.OpenReadStream()` | `getInputStream()` |
| `[FromForm] int? productId` | `@RequestParam Integer productId` |
| `TypedResults.File(stream, type, name)` | `ResponseEntity<Resource>` con `Content-Disposition` |

`file` es anulable (`IFormFile?`) para devolver nuestro propio mensaje si no llega, en
vez del error genérico del framework.

### `DisableAntiforgery()`

En .NET 8+, un endpoint que lee un formulario exige un **token antiforgery**, que
protege de CSRF: que otra web haga que tu navegador envíe un formulario usando tus
cookies. Esta API no se autentica con cookies, así que no aplica y se desactiva.
*En Spring sería el `csrf().disable()` habitual en APIs REST sin sesión.* Si algún día
se usan cookies de sesión, habría que volver a activarlo.

### Nunca fiarse del fichero que manda el usuario

| Dato del cliente | Riesgo | Qué hacemos |
|---|---|---|
| Nombre (`../../appsettings.json`) | *Path traversal*: escribir fuera de la carpeta | Se guarda con un GUID; el nombre original solo va a la base de datos |
| Extensión | Un `.exe` renombrado a `.pdf` | Se comprueban los primeros bytes (*magic number*) |
| `Content-Type` | Lo elige el cliente | Se ignora; se deduce de la extensión ya validada |
| Tamaño | Llenar el disco | Límite configurable (10 MB) y, por encima de 30 MB, Kestrel corta con 413 |

### Relación uno a muchos opcional en EF Core

```csharp
// Document
public int? ProductId { get; set; }      // int? = opcional (FK que admite NULL)
public Product? Product { get; set; }

// Product
public List<Document> Documents { get; } = [];

// DocumentConfiguration
builder.HasOne(d => d.Product)
    .WithMany(p => p.Documents)
    .HasForeignKey(d => d.ProductId)
    .OnDelete(DeleteBehavior.SetNull);
```

*Es el `@ManyToOne(optional = true)` + `@OneToMany(mappedBy = "product")` de JPA.* La
diferencia: EF Core **no carga la relación sola** (no hay *lazy loading* por defecto).
Para tener `document.Product` hay que pedirlo con `.Include(d => d.Product)`, que
genera un `LEFT JOIN`. *Es como un `JOIN FETCH` explícito.*

`OnDelete(SetNull)` se convierte en `ON DELETE SET NULL` en la clave foránea de
Postgres, así que funciona incluso con el `ExecuteDeleteAsync` del endpoint de
productos, que borra sin cargar nada en memoria.

### Interfaz para el almacenamiento

```csharp
public interface IDocumentStorage
{
    Task<string> SaveAsync(Stream content, string extension, CancellationToken ct);
    Stream? OpenRead(string storedFileName);
    void Delete(string storedFileName);
}

builder.Services.AddSingleton<IDocumentStorage, LocalDocumentStorage>();
```

Los endpoints no saben que es el disco. Para guardar en Azure Blob Storage o S3 basta
con otra implementación y cambiar una línea del registro.

### Options pattern

```json
"DocumentStorage": { "RootPath": "storage/documents", "MaxFileSizeMegabytes": 10 }
```

```csharp
builder.Services.AddOptions<DocumentStorageOptions>()
    .Bind(builder.Configuration.GetSection("DocumentStorage"))
    .ValidateDataAnnotations()   // [Required], [Range] de la clase
    .ValidateOnStart();          // si está mal, la app no arranca

// Donde se usa:
IOptions<DocumentStorageOptions> options  →  options.Value.MaxFileSizeBytes
```

*Es `@ConfigurationProperties(prefix = "document-storage")` + `@Validated`.* En los
tests se sustituye `RootPath` por una carpeta temporal, igual que la cadena de conexión.

### Fichero y base de datos: dos sitios que no comparten transacción

El fichero se escribe en disco y la fila en Postgres, y no se pueden hacer las dos
cosas de forma atómica. Por eso el orden importa:

- **Al subir**: primero el fichero y luego la fila. Si falla la base de datos, se
  borra el fichero para no dejarlo huérfano.
- **Al borrar**: primero la fila y luego el fichero. Si falla el borrado del fichero,
  queda un fichero sin usar, que es mejor que una fila que apunta a un fichero que no
  existe.

### Angular: `output()` y comunicación hijo → padre

```ts
// Hijo: DocumentUpload
readonly uploaded = output<UploadedDocument>();
this.uploaded.emit(document);
```

```html
<!-- Padre: DocumentList -->
<app-document-upload (uploaded)="onUploaded($event)" />
```

*Es la prop `onUploaded` de React.* El hijo no conoce la lista; solo avisa, y el padre
añade el documento con `documents.update(ds => [document, ...ds])`.

### Angular: `<input type="file">` y `viewChild`

Un input de fichero no funciona con `formControlName`: el fichero se lee del evento
(`(event.target as HTMLInputElement).files?.[0]`). Para vaciarlo tras subir hace falta
el elemento del DOM:

```ts
private readonly fileInput = viewChild.required<ElementRef<HTMLInputElement>>('fileInput');
this.fileInput().nativeElement.value = '';
```

*Es el `useRef` de React.*

### Angular: un pipe propio

```ts
@Pipe({ name: 'fileSize' })
export class FileSizePipe implements PipeTransform {
  transform(bytes: number): string { ... }   // 3482 → "3,4 KB"
}
```

```html
{{ document.sizeBytes | fileSize }}
```

Un pipe es una función de formato para el template; se añade a `imports` del
componente como cualquier otro. Usa `LOCALE_ID` para poner la coma decimal.

### Tests de subida de ficheros

- **Backend**: `MultipartFormDataContent` construye la misma petición que el navegador,
  y la factory de tests usa una carpeta temporal que se borra al terminar.
- **Frontend**: jsdom no deja asignar `input.files`, así que el test redefine la
  propiedad con `Object.defineProperty` y lanza el evento `change`.

## Archivos importantes

```
backend/src/DocAssist.Api/
├── appsettings.json                       ← sección DocumentStorage
├── Program.cs                             ← options, IDocumentStorage, MapDocumentEndpoints
├── Domain/Document.cs                     ← entidad
├── Domain/Product.cs                      ← + lista Documents
├── Data/Configurations/DocumentConfiguration.cs ← relación y SET NULL
├── Data/Migrations/..._AddDocuments.cs
└── Features/Documents/
    ├── DocumentEndpoints.cs               ← los 5 endpoints
    ├── DocumentFileRules.cs               ← reglas de los ficheros (código puro)
    ├── DocumentResponse.cs
    ├── DocumentStorageOptions.cs
    ├── IDocumentStorage.cs
    └── LocalDocumentStorage.cs

backend/tests/DocAssist.Api.Tests/Features/Documents/
├── DocumentFileRulesTests.cs              ← 13 tests unitarios
└── DocumentEndpointsTests.cs              ← 7 tests de integración

frontend/src/app/
├── core/api-errors.ts                     ← mensajes de un 400 (compartido)
└── features/documents/
    ├── uploaded-document.ts               ← interfaz y validación previa
    ├── document-service.ts
    ├── file-size-pipe.ts
    ├── document-upload/                   ← formulario de subida
    └── document-list/                     ← página /documents

infra/sample-documents/                     ← 4 documentos de ejemplo
```

Los ficheros subidos en desarrollo van a `backend/src/DocAssist.Api/storage/documents/`,
que está en `.gitignore`.

## Cómo probarlo

```powershell
# 1. Base de datos y API (desde la raíz)
docker compose up -d db
dotnet run --project backend/src/DocAssist.Api

# 2. Angular (en otro terminal)
cd frontend
npm start
```

En `http://localhost:4200/documents`:

1. Sube `infra/sample-documents/garantia.md`, `devoluciones.md` y `envios.md` sin producto:
   salen como **General**.
2. Sube `manual-playstation-5-slim.md` eligiendo *Sony PlayStation 5 Slim*.
3. Pulsa el nombre de un documento: se descarga con su nombre original.
4. Elige un `.txt`: el aviso sale antes de subir nada.
5. Renombra cualquier imagen a `.pdf` y súbela: el navegador la deja pasar, pero la API
   responde "The file is not a valid PDF.".
6. Borra un documento: desaparece de la lista y de la carpeta `storage/documents`.
7. Borra la PS5 en *Productos*: su manual pasa a **General**.

Con curl, sin el frontend:

```powershell
curl.exe -F "file=@infra/sample-documents/garantia.md" http://localhost:5080/api/documents
```

Tests:

```powershell
dotnet test backend/DocAssist.slnx      # 41 (necesita Docker)
cd frontend; npx ng test --watch=false  # 29
```

## Decisiones y alternativas

| Decisión | Por qué | Alternativas |
|---|---|---|
| Ficheros en disco y datos en Postgres | Lo pedía el plan; la base de datos no crece con binarios y es fácil de inspeccionar | Columna `bytea` en Postgres (todo en un sitio y con transacción, pero la base de datos crece mucho). Azure Blob Storage o S3 en producción |
| `IDocumentStorage` | Cambiar a la nube sin tocar los endpoints | Usar `File.*` directamente en el endpoint |
| Nombre en disco con GUID | Evita *path traversal* y nombres repetidos | Limpiar el nombre original, más frágil |
| Validar extensión **y** primeros bytes | La extensión la elige el usuario | Solo la extensión. Una librería de detección de tipos o un antivirus (ClamAV) en producción |
| Validación en una clase estática pura | Se prueba con tests unitarios sin HTTP ni disco | Validar dentro del endpoint |
| Errores como `ValidationProblem` | Mismo formato que los productos; el frontend reutiliza el mismo código | Un `BadRequest` con texto |
| `ON DELETE SET NULL` | El documento sigue siendo útil para el asistente aunque el producto ya no se venda | `CASCADE` (borraría las filas pero dejaría los ficheros en disco). `RESTRICT` y responder 409 al borrar un producto con documentos |
| `DisableAntiforgery()` | API sin cookies | Activar antiforgery y enviar el token desde Angular |
| Límite de 10 MB, máximo 25 configurable | Suficiente para manuales; por encima de 30 MB corta Kestrel | Subir el límite de Kestrel con `MaxRequestBodySize` |
| Formulario de subida como componente hijo con `output()` | Practicar la comunicación entre componentes; la lista no sabe cómo se sube | Todo en un componente |
| Interfaz `UploadedDocument` y no `Document` | `Document` ya es un tipo global del DOM | Llamarla `Document` y confiar en el `import` |
| Enlace `<a href>` para descargar | Lo descarga el navegador, sin cargar el fichero en memoria en JavaScript | `HttpClient` con `responseType: 'blob'` y un enlace temporal |
| Mensajes de la API en inglés | Igual que en los productos | Traducirlos |

## Para la entrevista

**Frases que puedo decir:**

> "Los documentos se suben como `multipart/form-data`. El backend recibe un `IFormFile`,
> valida extensión, tamaño y los primeros bytes del fichero, y lo guarda en disco con un
> GUID como nombre, nunca con el que manda el usuario."

> "El almacenamiento está detrás de una interfaz. Hoy es el disco local; en producción
> sería Blob Storage, y los endpoints no cambiarían."

> "La relación con producto es opcional: un manual pertenece a un producto y una
> política de devoluciones no. Si se borra el producto, el documento queda como general
> con `ON DELETE SET NULL`."

**Posibles preguntas:**

- *¿Por qué no te fías de la extensión ni del Content-Type?*
  Porque los decide el cliente. Cualquiera puede renombrar un ejecutable a `.pdf` o
  enviar el `Content-Type` que quiera con curl. Miro los primeros bytes del contenido.

- *¿Qué pasa si falla la base de datos después de guardar el fichero?*
  Borro el fichero en el `catch`. Disco y base de datos no comparten transacción, así
  que hay que compensar a mano. Para una solución más robusta habría un proceso que
  limpie los ficheros huérfanos.

- *¿Por qué los ficheros no van en la base de datos?*
  Se puede, con `bytea`, y tiene la ventaja de la transacción. Pero la base de datos
  crece mucho, las copias de seguridad son más lentas y no se puede servir el fichero
  desde un CDN. Lo habitual es un almacenamiento de objetos y guardar solo la referencia.

- *¿Qué es `Include` y por qué hace falta?*
  EF Core no carga las relaciones por defecto. `Include` hace un `JOIN` para traer el
  producto junto al documento en una sola consulta y evita el problema N+1.

- *¿Cómo pasa datos un componente hijo a su padre en Angular?*
  Con `output()`: el hijo emite y el padre escucha con `(evento)="..."`. Del padre al
  hijo se usa `input()`.
