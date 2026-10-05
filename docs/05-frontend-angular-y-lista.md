# 05 · Frontend Angular: proyecto, routing y lista de productos

## Qué hemos hecho

La aplicación Angular ya muestra el catálogo que devuelve la API:

- **Proyecto Angular 22** creado con `ng new` en `frontend/`: standalone components,
  Vitest para los tests y Prettier.
- **Layout mínimo**: una cabecera fija y un `<router-outlet />` donde el router pinta
  cada página.
- **Proxy de desarrollo**: el navegador solo habla con `localhost:4200`, y `ng serve`
  reenvía todo lo que empieza por `/api` a la API .NET en `localhost:5080`. Así
  evitamos CORS.
- **Modelo `Product`** en TypeScript, que refleja el `ProductResponse` del backend.
- **`ProductService`** inyectable (`@Service()`), que encapsula las llamadas HTTP.
- **Componente `ProductList`** cargado de forma *lazy* en `/products`. Gestiona el
  estado con signals (datos, cargando, error) y pinta una tabla con `@if` y `@for`.
- **Formato español** (`LOCALE_ID = 'es'`): los precios salen como `1.349,00 €`.
- **Test del `ProductService`** con `HttpTestingController`: comprueba la petición y
  la respuesta sin backend real.

Antes de empezar hubo que **renombrar la carpeta del proyecto**: el `#` de
`proyecto-C#-Angular` impedía arrancar Angular. Detalle en la entrada 08 de
`AI_REVIEW.md` y en la nota de `00-plan-y-progreso.md`.

## Conceptos nuevos

### Cómo arranca una app Angular

```
index.html  →  main.ts  →  app.config.ts  →  App (app.ts + app.html + app.css)
```

| Archivo | Qué hace | En React |
|---|---|---|
| `index.html` | Contiene `<app-root></app-root>`, donde se monta la app | `<div id="root">` |
| `main.ts` | `bootstrapApplication(App, appConfig)` | `createRoot(...).render(<App />)` |
| `app.config.ts` | Lista de *providers* globales: router, `HttpClient`, idioma | Los `<Provider>` que envuelven `<App />` |
| `App` | Componente raíz, que hace de layout | `App.tsx` con el layout |

### Componentes

Un componente es una **clase** con el decorador `@Component`, no una función:

```ts
@Component({
  imports: [RouterOutlet],      // lo que puede usar el template
  selector: 'app-root',         // la etiqueta HTML: <app-root />
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {}
```

- **`imports`**: importar algo en TypeScript **no basta**. Todo componente, directiva o
  pipe que use el template tiene que estar en este array, o la app no compila. Es el
  error más habitual al empezar.
- **Standalone**: desde Angular 20 todos los componentes declaran sus propias
  dependencias. Si ves `@NgModule` o `standalone: true`, es estilo antiguo.
- Un componente generado con el CLI tiene 4 archivos: `.ts` (lógica), `.html`
  (template), `.css` (estilos) y `.spec.ts` (test).

### CSS encapsulado frente a global

- El CSS de un componente (`app.css`) **solo afecta a ese componente**. Angular añade
  atributos `_ngcontent-...` a los elementos y reescribe los selectores. *Es lo mismo
  que consiguen los CSS Modules de React, pero escribiendo clases normales.*
- `styles.css` es global: fuentes, color de fondo, `body`.

### Proxy de desarrollo y CORS

El navegador bloquea las llamadas a otro origen (otro puerto cuenta como otro
origen) salvo que el servidor lo autorice con cabeceras CORS. Con el proxy, el
navegador pide `localhost:4200/api/products` y es `ng serve` quien reenvía la petición
a `localhost:5080`. **Entre servidores no hay CORS**, porque es una regla del navegador.

```
Navegador ──/api/products──▶ ng serve (:4200) ──reenvía──▶ API .NET (:5080)
```

*Es el `server.proxy` de `vite.config.ts`: Angular usa Vite por debajo.*

### Inyección de dependencias

```ts
@Service()   // singleton para toda la app
export class ProductService {
  private readonly http = inject(HttpClient);
}
```

*Es casi idéntica a Spring:* `@Service()` (nuevo en Angular 22) equivale a `@Service`
de Spring: un singleton disponible en toda la app, e `inject()` a la inyección por
constructor. Hasta Angular 21 se escribía `@Injectable({ providedIn: 'root' })`, que
sigue funcionando y verás en casi todo el código existente. La forma antigua de
inyectar, `constructor(private http: HttpClient)`, hace lo mismo que `inject()`.

### `HttpClient` y Observables

`http.get<Product[]>(url)` devuelve un **Observable**, no una Promise. La diferencia
clave es que **es perezoso**: la petición no sale hasta que alguien hace `.subscribe()`.

| | `fetch` / Promise | `HttpClient` / Observable |
|---|---|---|
| ¿Cuándo sale la petición? | Al llamar a `fetch()` | Al hacer `.subscribe()` |
| ¿Cómo obtengo el valor? | `await` o `.then()` | `.subscribe(valor => ...)` |
| ¿Se puede cancelar? | Con un `AbortController` | Cancelando la suscripción |

### Signals

| Angular | React | Para qué |
|---|---|---|
| `signal(valor)` | `useState` | Estado. Se lee con `products()` y se cambia con `products.set(...)` |
| `computed(() => ...)` | `useMemo` | Valor derivado. Detecta solo sus dependencias, sin lista |
| Código en el `constructor` | `useEffect(..., [])` | Cargar datos al crear el componente |
| `takeUntilDestroyed()` | La función de limpieza del `useEffect` | Cancelar la petición si el componente se destruye |

Cuando una signal cambia, Angular vuelve a pintar **solo** las partes del template
que la leen.

### Control de flujo en el template

```html
@if (loading()) { ... } @else if (error()) { ... } @else { ... }

@for (product of products(); track product.id) {
  <tr>...</tr>
} @empty {
  <tr><td colspan="5">No hay productos.</td></tr>
}
```

- `@if` sustituye a los `cond ? a : b` y `&&` del JSX.
- `@for` es el `.map()`. **`track` es obligatorio** y cumple la función de la `key` de React.
- `@empty` se pinta si el array está vacío.
- `{{ expr }}` interpola un valor, como el `{expr}` del JSX.

### Pipes

`{{ product.price | currency: 'EUR' }}` es una función de formato que se usa dentro
del template. Hay que añadirla a `imports` (`CurrencyPipe`). Formatea según el
`LOCALE_ID` de la app.

### Tests de servicios HTTP con `HttpTestingController`

En un test unitario no hay API ni base de datos. `provideHttpClientTesting()` cambia
la red por un backend falso que se controla desde el test:

```ts
TestBed.configureTestingModule({
  providers: [provideHttpClient(), provideHttpClientTesting()],
});
const httpMock = TestBed.inject(HttpTestingController);

service.getAll().subscribe((r) => (received = r)); // sale la petición (Observable perezoso)
const req = httpMock.expectOne('/api/products');    // exige justo una petición a esa URL
expect(req.request.method).toBe('GET');
req.flush(products);                                // responde con datos inventados
expect(received).toEqual(products);

afterEach(() => httpMock.verify()); // falla si queda alguna petición sin responder
```

| Angular | Spring | React |
|---|---|---|
| `TestBed.configureTestingModule` | El contexto de `@SpringBootTest` / `@WebMvcTest` | El *wrapper* de providers en `render()` |
| `HttpTestingController` | `MockRestServiceServer` | MSW |
| `expectOne(url)` | `expect(requestTo(url))` | `http.get(url, ...)` en un *handler* |
| `req.flush(datos)` | `andRespond(withSuccess(...))` | `HttpResponse.json(datos)` |
| `httpMock.verify()` | `server.verify()` | `onUnhandledRequest: 'error'` |

Los tests corren con **Vitest** (`@angular/build:unit-test`), no con Karma y Jasmine
como en versiones antiguas. `describe`, `it` y `expect` funcionan igual que en Jest.

Desde Angular 21, `HttpClient` está disponible sin `provideHttpClient()`. Aun así,
el test debe registrar `provideHttpClientTesting()`; sin él, las peticiones irían a
la red real (entrada 11 de `AI_REVIEW.md`).

### Rutas y lazy loading

```ts
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'products' },
  {
    path: 'products',
    loadComponent: () =>
      import('./features/products/product-list/product-list').then((m) => m.ProductList),
    title: 'Productos · Tienda Tech',
  },
];
```

- Las rutas son **un array de objetos**, no JSX.
- `pathMatch: 'full'` hace que `''` solo coincida con la URL vacía. Sin él, `''`
  coincide con el principio de cualquier URL y siempre redirigiría.
- `loadComponent` con `import()` separa el componente en su propio *chunk*, que solo se
  descarga al visitar la ruta. *Es `React.lazy`, pero sin `<Suspense>`.* En la
  compilación aparece en `Lazy chunk files`.
- `title` cambia el título de la pestaña.

## Archivos importantes

```
frontend/
├── angular.json                 ← configuración del CLI (proxyConfig en "serve")
├── proxy.conf.json              ← /api → http://localhost:5080
├── CLAUDE.md                    ← buenas prácticas de Angular, generado por ng new
└── src/
    ├── index.html               ← <app-root>, lang="es"
    ├── main.ts                  ← bootstrapApplication
    ├── styles.css               ← estilos globales
    └── app/
        ├── app.config.ts        ← router, HttpClient, LOCALE_ID
        ├── app.routes.ts        ← rutas
        ├── app.ts / .html / .css   ← layout: cabecera + router-outlet
        └── features/products/
            ├── product.ts           ← interfaz Product y tipo ProductCategory
            ├── product-service.ts   ← llamadas a /api/products
            ├── product-service.spec.ts ← test con HttpTestingController
            └── product-list/        ← componente de la lista
```

## Cómo probarlo

```powershell
# 1. Base de datos y API (desde la raíz)
docker compose up -d db
dotnet run --project backend/src/DocAssist.Api

# 2. Angular (en otro terminal)
cd frontend
npm start
```

- `http://localhost:4200/` redirige a `/products` y muestra "10 productos" y la tabla.
- `http://localhost:4200/api/products` devuelve el JSON de la API a través del proxy.
- Si paras la API y recargas, aparece el mensaje de error en rojo, y en el terminal de
  `npm start` un error de proxy `ECONNREFUSED`.
- Con DevTools → *Network* → *Slow 3G* se ve el mensaje "Cargando productos…".
- En DevTools → *Network* → *JS* se ve el *chunk* de `product-list` descargándose aparte.

Tests:

```powershell
cd frontend
npx ng test --watch=false
```

Son 4 tests: los de `App` y `ProductList` que genera el CLI (el de `App` adaptado al
texto nuevo) y el de `ProductService`, que comprueba que `getAll()` hace
`GET /api/products` y devuelve la lista tal cual.

Para ver que el test de verdad comprueba algo, cambia la URL del servicio a
`/api/productos`: falla con `Expected one matching request for criteria "Match URL:
/api/products", found none. Requests received are: GET /api/productos.`

## Decisiones y alternativas

| Decisión | Por qué | Alternativas |
|---|---|---|
| Proxy de desarrollo | El código usa URLs relativas (`/api/...`) que valen igual en desarrollo y en producción (nginx en el paso 12). No abre CORS en el backend | Configurar CORS en la API y usar URLs absolutas por entorno |
| Organización por *features* (`features/products`) | Igual que el backend (`Features/Products`): todo lo de una funcionalidad junto | Carpetas por tipo (`components/`, `services/`, `models/`) |
| `product-service.ts` en vez de `product.ts` | Desde Angular 20 el CLI no añade el sufijo `.service`; `product.ts` chocaría con la interfaz | `product.service.ts` con el estilo antiguo |
| Interfaz escrita a mano | Para entender la correspondencia C# → TypeScript | Generarla desde el OpenAPI (`openapi-typescript`, NSwag) |
| `ProductCategory` como *union type* | Coincide con los textos del JSON y no genera JavaScript | `enum` de TypeScript |
| `releaseDate` como `string` | JSON no tiene tipo fecha; se convertirá solo al mostrarla | Convertirla a `Date` en el servicio |
| Servicio que devuelve Observables y estado en el componente con signals | Patrón explícito, parecido a `useState` + `useEffect`, fácil de seguir | `toSignal()` u `httpResource()`, más concisos, que ocultan los estados de carga y error |
| Solo `getAll()` en el servicio | Los demás métodos llegarán con el formulario del paso 6 | Escribir todo el CRUD por adelantado |
| `@Service()` en el servicio | Lo recomienda el `CLAUDE.md` del proyecto para servicios nuevos en Angular 22 | `@Injectable({ providedIn: 'root' })`, equivalente y más extendido |
| Test del servicio con `HttpTestingController` | Comprueba la URL, el método y la respuesta sin levantar el backend | Sustituir `HttpClient` por un *mock* a mano con `vi.fn()`, que no comprueba la petición real |
| Lazy loading de la lista | Práctica recomendada; carga inicial más ligera | `component: ProductList`, con carga inmediata |
| `LOCALE_ID = 'es'` | Precios y fechas en formato español | Indicar el locale en cada pipe |
| Etiquetas semánticas (`header`, `main`, `th scope="col"`) | Accesibilidad: los lectores de pantalla las usan para orientarse | `div` genéricos |

## Para la entrevista

**Frases que puedo decir:**

> "El frontend usa standalone components y signals, que es el Angular moderno. Cada
> componente declara en `imports` lo que usa su template, y el estado son signals:
> `signal` para el estado, `computed` para lo derivado, y Angular solo vuelve a pintar
> lo que depende de lo que cambió."

> "En desarrollo uso el proxy de `ng serve`, así que el navegador solo habla con un
> origen y no hace falta CORS. El código usa URLs relativas y en producción funcionará
> igual detrás de nginx."

> "Los componentes no llaman a HTTP directamente: lo hacen a través de un servicio
> inyectable, que es un singleton con `@Service()`. Es la misma separación que
> `@Controller` y `@Service` en Spring, y permite sustituir el servicio en los tests."

**Posibles preguntas:**

- *¿Qué diferencia hay entre un Observable y una Promise?*
  El Observable es perezoso (no hace nada hasta que te suscribes), puede emitir varios
  valores y se puede cancelar. La Promise se ejecuta al crearse y da un único valor.

- *¿Qué aportan las signals frente a la detección de cambios clásica?*
  Angular sabe exactamente qué partes del template dependen de cada signal, así que no
  tiene que revisar todo el árbol de componentes en cada evento. Con signals se puede
  prescindir de zone.js.

- *¿Para qué sirve `track` en `@for`?*
  Para que Angular identifique cada elemento entre renderizados y reutilice su DOM en
  lugar de recrearlo. Es la `key` de React.

- *¿Qué es el lazy loading y cómo lo has aplicado?*
  Separar el código de una ruta en un archivo aparte que solo se descarga al visitarla.
  Lo hago con `loadComponent` y un `import()` dinámico.

- *¿Por qué la interfaz `Product` no te protege si la API cambia?*
  Porque los tipos de TypeScript desaparecen al compilar. La interfaz es una promesa
  sobre la forma del JSON, no una validación. En proyectos grandes se genera desde el
  OpenAPI del backend para que no se desincronice.

- *¿Cómo pruebas un servicio que hace llamadas HTTP?*
  Con `provideHttpClientTesting()` y `HttpTestingController`. El test se suscribe,
  comprueba con `expectOne` la URL y el método, responde con `flush` y verifica lo
  recibido. Al final, `verify()` asegura que no quedan peticiones sin responder. Es
  como `MockRestServiceServer` en Spring.
