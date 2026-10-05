# Registro de revisión del código generado con IA

Este documento recoge cada vez que el código propuesto por el asistente tuvo que
corregirse: porque yo lo rechacé, porque lo modifiqué, o porque se detectó un
problema después de escribirlo.

El objetivo no es demostrar que la IA se equivoca, sino demostrar el hábito de
**revisar críticamente** lo que genera antes de darlo por bueno.

## Formato de cada entrada

```
### NN - Título corto  (paso N)
- **Qué se generó:** ...
- **Qué problema tenía:** ...
- **Cómo se corrigió:** ...
- **Qué aprendí:** (opcional)
```

---

## Entradas

### 01 - Rama por defecto `master` en lugar de `main`  (paso 1)
- **Qué se generó:** `git init -b main` combinado con `mkdir` en la misma línea.
- **Qué problema tenía:** el comando se ejecutó sobre un repositorio ya
  inicializado, así que git ignoró `--initial-branch` con un aviso y la rama
  quedó como `master`. El aviso se pasó por alto al leer solo el final de la
  salida.
- **Cómo se corrigió:** se comprobó la rama activa con `git branch --show-current`
  y se renombró con `git branch -m master main`.
- **Qué aprendí:** conviene verificar el estado resultante, no solo asumir que el
  comando hizo lo que pedía la bandera.

### 02 - El asistente iba a crear todo el backend de golpe  (paso 2)
- **Qué se generó:** un único comando que creaba la solución, el proyecto de la
  API y el de tests a la vez, ejecutado directamente por el asistente.
- **Qué problema tenía:** el código habría sido correcto, pero yo no habría
  entendido qué hacía cada comando ni por qué. En un proyecto para aprender, que
  la IA lo haga todo sola es un problema aunque el resultado funcione.
- **Cómo se corrigió:** rechacé la ejecución y cambié la forma de trabajar: los
  comandos importantes los ejecuto yo, en tramos pequeños, después de que el
  asistente me explique qué hacen. El paso 2 se hizo en cinco tramos.
- **Qué aprendí:** la IA acelera mucho, pero hay que decidir en qué partes
  conviene delegar y en cuáles no. Lo que no entiendo no lo puedo defender en una
  entrevista ni mantener después.

### 03 - Las instrucciones del cambio de dominio daban por hecho cosas que no existían  (entre pasos 2 y 3)
- **Qué se generó:** para pasar de equipos industriales a una tienda de
  tecnología, pedí borrar las migraciones y regenerarlas, renombrar DTOs y
  endpoints, adaptar la entidad `Document` y sustituir los datos de ejemplo.
- **Qué problema tenía:** nada de eso existía todavía: ni migraciones, ni DTOs, ni
  endpoints, ni `Document`, ni *seed*. Ejecutado al pie de la letra, el subagente
  habría creado la primera migración (que es parte del paso 3, y la hago yo) o
  inventado estructuras para poder "renombrarlas".
- **Cómo se corrigió:** antes de lanzar el subagente se contrastaron las
  instrucciones con el estado real del repositorio y se le pasó un inventario
  explícito de lo que había y lo que no. Lo que no existía quedó anotado como
  pendiente en su paso (*seed* en el paso 3, `Document` con relación opcional en
  el paso 7).
- **Qué aprendí:** una instrucción a una IA puede estar mal aunque el objetivo sea
  correcto. Hay que comprobar sus premisas contra el código antes de ejecutarla.

### 04 - Ejemplo falso para justificar las especificaciones anulables  (entre pasos 2 y 3)
- **Qué se generó:** el comentario de `Product.cs` y el documento `02b` justificaban
  que `ScreenInches` fuese anulable con "una consola no tiene pantalla". El
  ejemplo venía de las instrucciones que el asistente principal le dio al
  subagente, y este lo repitió en tres sitios.
- **Qué problema tenía:** es falso. La Nintendo Switch tiene pantalla, y es
  precisamente un producto que entrará en los datos de ejemplo. Además, el error
  escondía el argumento de verdad: la especificación no depende de la categoría,
  sino de cada producto.
- **Cómo se corrigió:** se cambió el ejemplo a "una PS5 no tiene pantalla, una
  Nintendo Switch sí" y se añadió que por eso no se puede decidir por categoría.
  En la misma revisión se matizó otra afirmación exagerada del documento: decía
  que EF Core "traduce peor" las columnas JSONB, cuando en realidad las mapea con
  `ToJson()`; el argumento correcto es que para tres campos no compensa la
  complejidad.
- **Qué aprendí:** revisar lo que genera un subagente también significa revisar lo
  que le pedí. Un error en las instrucciones se multiplica en todos los sitios
  donde el subagente lo aplica.

### 05 - `git commit --amend` sobre un commit que ya estaba en GitHub  (entre pasos 2 y 3)
- **Qué se generó:** para mantener el cambio de dominio en un único commit, el
  asistente añadió las correcciones de la revisión con `git commit --amend`,
  dando por hecho que el commit del subagente no se había subido.
- **Qué problema tenía:** sí estaba subido. El *amend* creó un commit distinto que
  solo habría entrado con `git push --force`, reescribiendo historia pública.
- **Cómo se corrigió:** el push se rechazó. Se inspeccionó el commit remoto con
  `git fetch` y `git log HEAD..origin/main`, se volvió a él con
  `git reset --soft origin/main` (sin perder cambios) y las correcciones fueron en
  un commit nuevo encima. No se usó `--force`.
- **Qué aprendí:** antes de reescribir un commit hay que comprobar si ya es
  público (`git fetch` + `git status`), no suponerlo. Y un push rechazado es una
  señal para investigar, no un obstáculo que saltarse con `--force`.

### 06 - Instrucciones de terminal que no coincidían con la realidad  (paso 3)
- **Qué se generó:** dos indicaciones del asistente sobre comandos:
  1. Al explicar `dotnet ef database update`, anunció que en la salida se verían
     el `CREATE TABLE` y los `INSERT` del *seed*.
  2. En el primer borrador de `docs/03`, para "empezar de cero con la base de
     datos" propuso `docker compose down -v`.
- **Qué problema tenía:**
  1. `dotnet ef` no muestra el SQL por defecto (hace falta `--verbose`). En
     cambio, sí salió un `fail` que nadie había anunciado y que parecía un
     error: el `SELECT` sobre `__EFMigrationsHistory` en una base vacía.
  2. `down -v` borra **todos** los volúmenes, incluidos los ~5 GB de modelos de
     Ollama, para algo que solo necesitaba rehacer la base de datos.
- **Cómo se corrigió:**
  1. Se explicó por qué el `fail` es inofensivo y se comprobó el resultado real
     en la base (migración registrada y 10 productos), en vez de dar por buena
     la salida esperada. El `fail` quedó documentado en "Cómo probarlo".
  2. Se sustituyó por `dotnet ef database drop --force` + `database update`,
     que solo afecta a la base de datos de la aplicación.
- **Qué aprendí:** describir la salida esperada de un comando es una afirmación
  que se puede comprobar, y hay que comprobarla. Y antes de proponer un comando
  destructivo hay que mirar exactamente qué borra.

### 07 - `UseExceptionHandler()` convertía errores del cliente en 500  (paso 4)
- **Qué se generó:** `app.UseExceptionHandler()` sin opciones, para devolver
  cualquier excepción no controlada como un 500 con ProblemDetails.
- **Qué problema tenía:** al probar un JSON mal formado y una categoría
  inexistente (`"Tablet"`), la API respondía **500**. En desarrollo, las Minimal
  APIs lanzan `BadHttpRequestException` cuando no pueden leer la petición, y el
  gestor la trataba como un fallo del servidor. Además, en producción no se lanza
  esa excepción, así que el mismo error daba 500 en desarrollo y 400 en
  producción. El asistente sospechaba el problema (pidió que se le avisara si
  salía un 500), pero no lo resolvió de antemano.
- **Cómo se corrigió:** se añadió un `StatusCodeSelector` que usa el código que
  trae la propia `BadHttpRequestException` (400, o 413 si el cuerpo es demasiado
  grande) y deja el 500 para el resto de excepciones.
- **Qué aprendí:** probar los casos de error, no solo el camino feliz. Y si se
  sospecha de un fallo, es mejor comprobarlo antes de dar el código por bueno
  que esperar a que aparezca.

### 08 - "El `#` en el nombre de la carpeta no es problema"  (paso 1, detectado en el paso 5)
- **Qué se generó:** en el paso 1, cuando GitHub convirtió el `#` del nombre del
  repositorio en `--`, el asistente afirmó que la carpeta local podía seguir
  llamándose `proyecto-C#-Angular` "sin problema".
- **Qué problema tenía:** cuatro pasos después, `ng serve` no pudo arrancar. Angular
  usa Vite, y Vite convierte las rutas de archivo en URLs; en una URL, el `#` marca
  el inicio del fragmento y el resto de la ruta se descarta. El error lo mostraba:
  buscaba archivos en `C:/dev/PERSONAL/proyecto-C`. El propio Vite avisaba del `#`.
  Que GitHub rechazara el carácter ya era una señal de que daría problemas en otras
  herramientas.
- **Cómo se corrigió:** se renombró la carpeta a `proyecto-dotnet-angular`. Antes
  se fijó `name: proyecto-c-angular` en `docker-compose.yml`, porque Compose usa el
  nombre de la carpeta para nombrar los volúmenes y, sin eso, habría creado
  volúmenes nuevos y vacíos (perdiendo la base de datos y 5 GB de modelos). También
  se actualizaron las rutas de la documentación y se copió la memoria del asistente
  a la ruta nueva.
- **Qué aprendí:** los caracteres especiales en rutas (`#`, espacios, tildes) son
  una fuente conocida de fallos en herramientas de desarrollo. Cuando una
  herramienta rechaza un nombre, hay que preguntarse si las demás lo aceptarán. Y
  un cambio que parece cosmético, como renombrar una carpeta, puede tener efectos
  ocultos, como los volúmenes de Docker.

### 09 - Ruta de import incorrecta para el componente generado  (paso 5)
- **Qué se generó:** en las instrucciones del sub-paso 6a, el asistente dio la ruta
  `import('./features/products/product-list')` para la carga *lazy* de `ProductList`,
  suponiendo que `ng generate component` crearía los archivos directamente en
  `features/products/`.
- **Qué problema tenía:** el CLI crea cada componente **en su propia subcarpeta**
  (`features/products/product-list/product-list.ts`). Con la ruta propuesta, la app
  no habría compilado.
- **Cómo se corrigió:** al ejecutar el comando, la salida (`CREATE .../product-list/...`)
  mostró la subcarpeta, y la ruta se cambió a
  `./features/products/product-list/product-list` antes de escribir `app.routes.ts`.
- **Qué aprendí:** leer la salida de los generadores antes de escribir código que
  depende de dónde dejan los archivos, en lugar de dar por hecha la estructura.

### 10 - `@Injectable` cuando el proyecto pedía `@Service`  (paso 5)
- **Qué se generó:** el `ProductService` se explicó y se escribió con
  `@Injectable({ providedIn: 'root' })`, la forma clásica de crear un servicio singleton.
- **Qué problema tenía:** el `CLAUDE.md` del frontend, generado por `ng new` con las
  buenas prácticas de Angular 22, dice que en los servicios singleton nuevos se prefiere
  el decorador **`@Service()`**, nuevo en Angular 22. El asistente no había leído ese
  archivo entero antes de proponer el servicio. Funcionar, funciona: `@Injectable`
  sigue siendo válido.
- **Cómo se corrigió:** se detectó al documentar el paso. En el cierre del paso 5 se
  cambió a `@Service()`; los tests y la compilación siguieron en verde.
- **Qué aprendí:** leer las reglas del proyecto (`CLAUDE.md`) antes de generar código,
  sobre todo en un framework que cambia tan rápido como Angular.

### 11 - Predicción equivocada: "el test generado fallará sin `provideHttpClient`"  (paso 5)
- **Qué se generó:** al explicar el sub-paso 7, el asistente afirmó que el
  `product-service.spec.ts` creado por `ng generate` estaría fallando con
  `No provider for HttpClient`, porque el módulo de test estaba vacío.
- **Qué problema tenía:** era falso. Desde Angular 21, `HttpClient` está disponible
  por defecto en el inyector raíz sin llamar a `provideHttpClient()`. Al ejecutar
  `ng test`, los 4 tests pasaron.
- **Cómo se corrigió:** se ejecutaron los tests antes de tocar nada y se vio que estaban
  en verde. El test nuevo registra igualmente `provideHttpClient()` y
  `provideHttpClientTesting()`, porque sin el segundo las peticiones irían a la red real.
- **Qué aprendí:** comprobar el estado real antes de diagnosticar un fallo, y no
  dar por buenas reglas de versiones anteriores del framework.
