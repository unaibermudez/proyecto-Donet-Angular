# 06 · Formularios reactivos: crear, editar y borrar productos

## Qué hemos hecho

El frontend ya cubre el CRUD completo contra la API:

| Pantalla | Ruta | Qué hace |
|---|---|---|
| Lista | `/products` | Botón **Nuevo producto**, y en cada fila **Editar** y **Borrar** (con confirmación) |
| Alta | `/products/new` | Formulario vacío; al guardar hace `POST` y vuelve a la lista |
| Edición | `/products/:id/edit` | Carga el producto con `GET`, rellena el formulario y al guardar hace `PUT` |

Y alrededor:

- **`ProductService`** con todo el CRUD: `getAll`, `getById`, `create`, `update` y `delete`.
- **Formulario reactivo tipado** con las mismas reglas que `ProductRequest` en el
  backend, incluidos dos validadores propios: número entero y fecha como mucho a un
  año vista.
- **Mensajes de error por campo**, que aparecen al salir del campo o al intentar
  guardar, enlazados al campo con `aria-describedby` y `aria-invalid`.
- **Errores del servidor**: si la API devuelve un 400 con ProblemDetails, sus
  mensajes se enseñan encima de los botones.
- **Estados de carga**: "Cargando producto…", botón "Guardando…" desactivado,
  botón "Borrando…" en la fila.
- **Tests**: 15 en total. Nuevos: 4 del servicio (uno por método), 5 del formulario
  y 3 de la lista.

## Conceptos nuevos

### Formularios reactivos

El formulario se define **en la clase**, no en el HTML. El template solo se engancha a
él con `formGroup` y `formControlName`:

```ts
private readonly fb = inject(NonNullableFormBuilder);

protected readonly form = this.fb.group({
  name: ['', [Validators.required, Validators.maxLength(200)]],
  category: this.fb.control<ProductCategory | ''>('', Validators.required),
  price: this.fb.control<number | null>(null, [Validators.required, Validators.min(0.01)]),
  // ...
});
```

```html
<form [formGroup]="form" (ngSubmit)="save()" novalidate>
  <input id="name" type="text" formControlName="name" />
</form>
```

| Angular | react-hook-form + Zod | Spring |
|---|---|---|
| `FormGroup` (`fb.group`) | `useForm()` | El objeto con `@Valid` |
| `FormControl` (cada campo) | `register('name')` | Cada propiedad del DTO |
| `Validators.required`, `min`, `maxLength` | `z.string().min(1)`, `z.number().min()` | `@NotBlank`, `@Min`, `@Size` |
| Validador propio (función) | `.refine(...)` | Un `ConstraintValidator` propio |
| `form.invalid`, `control.errors`, `control.touched` | `formState.isValid`, `errors`, `touchedFields` | `BindingResult` |
| `form.getRawValue()` | `handleSubmit(values => ...)` | El DTO ya relleno |
| `form.patchValue(product)` | `reset(product)` | — |

- **`NonNullableFormBuilder`**: con el `FormBuilder` normal, `form.reset()` deja los
  campos a `null` y TypeScript obliga a tratar `null` en todos. Con este, cada campo
  vuelve a su valor inicial y los tipos son más limpios.
- **Formularios tipados**: `form.controls.price` es `FormControl<number | null>`. Si
  escribes `form.controls.prize`, no compila.
- **`<input type="number">`** entrega un `number` (o `null` si está vacío), no texto.
  **`<input type="date">`** entrega `"AAAA-MM-DD"`, que es justo lo que espera el
  `DateOnly` del backend.
- **`novalidate`**: desactiva los globos de validación del navegador para que los
  mensajes los ponga Angular, todos con el mismo estilo.

### Validadores propios

Un validador es una función que recibe el control y devuelve `null` si es válido, o
un objeto con el error:

```ts
export function integer(control: AbstractControl): ValidationErrors | null {
  const value = control.value;
  if (value === null || value === '') return null; // vacío: lo decide required
  return Number.isInteger(value) ? null : { integer: true };
}
```

Están en `product-validators.ts`. Repiten dos reglas del backend: los campos `int`
no aceptan decimales, y la fecha de lanzamiento no puede estar a más de un año vista.

### Cuándo enseñar los errores

Enseñar "Este campo es obligatorio" en un formulario recién abierto queda mal. Por eso
`errorFor()` solo devuelve el mensaje si el campo está **`touched`** (el usuario ha
entrado y salido) o si ya se ha intentado guardar (signal `submitted`).

### Validar en los dos lados

La validación del cliente es para la **experiencia de usuario**: respuesta inmediata
sin ir al servidor. La del servidor es la que **protege los datos**, porque cualquiera
puede llamar a la API sin pasar por Angular. Si el servidor rechaza algo que el cliente
dio por bueno, sus mensajes de ProblemDetails se enseñan igualmente:

```ts
if (error.status === 400 && error.error?.errors) {
  return Object.values(error.error.errors as Record<string, string[]>).flat();
}
```

### Parámetros de ruta como `input()`

```ts
provideRouter(routes, withComponentInputBinding())   // app.config.ts

readonly id = input<string>();                        // product-form.ts
protected readonly isEdit = computed(() => this.id() !== undefined);
```

Con `withComponentInputBinding()`, el `:id` de `/products/:id/edit` llega como un
`input()` del componente, sin inyectar `ActivatedRoute`. *Es el `useParams()` de React
Router, o `@PathVariable` en Spring.* Un mismo componente sirve para crear y editar:
sin `id`, crea.

Los inputs todavía no tienen valor en el constructor; por eso el producto se carga en
**`ngOnInit`**.

### Navegar desde código

```ts
private readonly router = inject(Router);
this.router.navigate(['/products']);
```

*Es el `navigate('/products')` de `useNavigate()` en React.* En el template se usa
`routerLink`, que es el `<Link to>` de React Router:

```html
<a [routerLink]="['/products', product.id, 'edit']">Editar</a>
```

### Actualizar una signal a partir de su valor anterior

```ts
this.products.update((products) => products.filter((p) => p.id !== product.id));
```

*Es el `setProducts(prev => prev.filter(...))` de React.* Tras borrar, el producto se
quita de la lista en memoria sin volver a pedirla entera a la API.

### `takeUntilDestroyed` fuera del constructor

En el constructor basta con `takeUntilDestroyed()`. En un método (guardar, borrar) ya
no estamos en el "contexto de inyección", así que hay que pasarle el `DestroyRef`:

```ts
private readonly destroyRef = inject(DestroyRef);
save$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(...)
```

### Tests de componentes con `TestBed`

Los tests del formulario y de la lista **sustituyen el servicio por uno falso**, y el
test interactúa con el HTML como lo haría el usuario:

```ts
productService = { getById: vi.fn(), create: vi.fn(), update: vi.fn() };

TestBed.configureTestingModule({
  imports: [ProductForm],
  providers: [provideRouter([]), { provide: ProductService, useValue: productService }],
});

fill('name', 'Galaxy S25');                                  // escribe y lanza 'input'
element.querySelector('button[type="submit"]')!.click();
await fixture.whenStable();                                  // espera a que Angular pinte
expect(productService.create).toHaveBeenCalledWith(request);
```

| Angular + Vitest | Spring | React |
|---|---|---|
| `{ provide: ProductService, useValue: falso }` | `@MockBean` | `vi.mock('./productService')` |
| `vi.fn().mockReturnValue(of(datos))` | `when(...).thenReturn(...)` | `vi.fn().mockResolvedValue(datos)` |
| `fixture.componentRef.setInput('id', '1')` | — | `render(<ProductForm id="1" />)` |
| `fixture.whenStable()` | — | `await waitFor(...)` / `findBy...` |
| `vi.spyOn(window, 'confirm').mockReturnValue(true)` | — | lo mismo |

`of(datos)` crea un Observable que emite al momento: así el servicio falso responde
sin HTTP. `throwError(() => error)` simula una respuesta de error.

## Archivos importantes

```
frontend/src/
├── styles.css                     ← botones compartidos (.button) y .visually-hidden
└── app/
    ├── app.config.ts              ← withComponentInputBinding()
    ├── app.routes.ts              ← /products/new y /products/:id/edit (lazy)
    └── features/products/
        ├── product.ts             ← + ProductRequest y categoryLabels
        ├── product-service.ts     ← CRUD completo
        ├── product-validators.ts  ← integer y notMoreThanOneYearAhead
        ├── product-form/          ← formulario de alta y edición (+ 5 tests)
        └── product-list/          ← + Editar, Borrar y Nuevo producto (+ 3 tests)
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

En `http://localhost:4200/products`:

1. **Nuevo producto** → pulsa **Guardar** con el formulario vacío: salen los 7
   mensajes de los campos obligatorios y no se llama a la API (DevTools → *Network*).
2. Rellénalo con un stock de `1.5`: "Tiene que ser un número entero".
3. Rellénalo bien y guarda: vuelve a la lista y el producto aparece.
4. **Editar** en esa fila: el formulario sale relleno; cambia el stock y guarda.
5. **Borrar**: pide confirmación. Con *Cancelar* no pasa nada; con *Aceptar*
   desaparece de la lista.
6. `http://localhost:4200/products/9999/edit`: "El producto no existe."
7. Para ver un error que **solo detecta el servidor**, comenta `notMoreThanOneYearAhead`
   en `product-form.ts`, pon una fecha de 2099 y guarda: sale el mensaje en inglés de
   la API.

Tests:

```powershell
cd frontend
npx ng test --watch=false
```

Son 15: 2 de `App`, 5 de `ProductService`, 5 de `ProductForm` y 3 de `ProductList`.

## Decisiones y alternativas

| Decisión | Por qué | Alternativas |
|---|---|---|
| Formularios reactivos | Es lo que pedía el plan y lo que hay en la mayoría de proyectos Angular existentes, así que es lo que más se pregunta en una entrevista | **Signal Forms** (`@angular/forms/signals`), estables en Angular 22 y los que recomienda el `CLAUDE.md` para formularios nuevos. Formularios por template (`ngModel`), más parecidos a Angular 1 |
| Un componente para crear y editar | Los campos y las reglas son los mismos; solo cambia si hay `id` | Dos componentes que compartan el formulario |
| Reglas del backend repetidas en el cliente | Respuesta inmediata al usuario | Validar solo en el servidor y enseñar sus errores. Generar las reglas desde el OpenAPI |
| Errores al salir del campo o al guardar | No asustar con errores en un formulario recién abierto | Errores al escribir (más agresivo) o solo al guardar |
| `id` como `input()` con `withComponentInputBinding()` | Menos código y el componente se prueba con `setInput` | `inject(ActivatedRoute).snapshot.paramMap` |
| `confirm()` del navegador para borrar | Simple y accesible por defecto | Un diálogo propio con `<dialog>` o Angular CDK, más bonito pero con gestión del foco a mano |
| Quitar de la lista tras borrar, sin recargarla | Una petición menos | Volver a llamar a `getAll()` |
| Mensajes de error de la API en inglés | El backend se escribió en inglés (código en inglés) | Traducirlos en el backend o mapear los nombres de campo en el cliente |
| Servicio falso en los tests de componentes | Se prueba el componente solo; el HTTP ya lo prueba el test del servicio | `HttpTestingController` también aquí, que mezcla las dos cosas |

## Para la entrevista

**Frases que puedo decir:**

> "Uso formularios reactivos tipados: el formulario se define en la clase con
> `NonNullableFormBuilder`, y TypeScript sabe el tipo de cada campo. Las reglas son las
> mismas que en el DTO del backend, pero la validación que manda es la del servidor;
> la del cliente es para dar respuesta inmediata."

> "Un mismo componente sirve para crear y editar. El `id` de la ruta llega como un
> `input()` gracias a `withComponentInputBinding()`; si no hay `id`, crea."

> "En los tests de componentes sustituyo el servicio por uno falso con `vi.fn()` y
> pruebo como un usuario: relleno los campos, pulso el botón y compruebo qué se le
> pidió al servicio y qué se ve en pantalla."

**Posibles preguntas:**

- *¿Reactivos o por template?*
  Reactivos: el formulario está en el código, es tipado, fácil de probar y de
  componer. Los de template (`ngModel`) sirven para formularios muy simples.

- *¿Has oído hablar de Signal Forms?*
  Sí, son la nueva API de Angular 22, basada en signals y con validación por esquema.
  Para un proyecto nuevo serían la opción recomendada; he usado los reactivos porque son
  lo que hay en la mayoría de código existente y quería entenderlos primero.

- *Si ya validas en el cliente, ¿hace falta validar en el servidor?*
  Siempre. Cualquiera puede llamar a la API con `curl`. La validación del cliente es
  experiencia de usuario, no seguridad.

- *¿Qué diferencia hay entre `touched` y `dirty`?*
  `touched`: el usuario ha entrado y salido del campo. `dirty`: ha cambiado su valor.

- *¿Qué es `patchValue` frente a `setValue`?*
  `setValue` exige todos los campos exactos; `patchValue` acepta un objeto parcial e
  ignora lo que sobra (como el `id` del producto).
