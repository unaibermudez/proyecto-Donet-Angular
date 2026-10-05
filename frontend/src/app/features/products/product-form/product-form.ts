import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, DestroyRef, inject, input, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { validationMessages } from '../../../core/api-errors';
import { categoryLabels, ProductCategory, ProductRequest } from '../product';
import { ProductService } from '../product-service';
import { integer, notMoreThanOneYearAhead } from '../product-validators';

// Formulario para crear (/products/new) y editar (/products/:id/edit) un producto.
@Component({
  imports: [ReactiveFormsModule, RouterLink],
  selector: 'app-product-form',
  styleUrl: './product-form.css',
  templateUrl: './product-form.html',
})
export class ProductForm implements OnInit {
  private readonly productService = inject(ProductService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(NonNullableFormBuilder);

  // El :id de la ruta, gracias a withComponentInputBinding(). Sin id, el formulario crea.
  readonly id = input<string>();
  protected readonly isEdit = computed(() => this.id() !== undefined);

  protected readonly loading = signal(false);
  protected readonly loadError = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly saveErrors = signal<string[]>([]);
  // Hasta el primer intento de guardar, los errores solo se ven en los campos ya visitados.
  protected readonly submitted = signal(false);

  protected readonly categoryLabels = categoryLabels;
  protected readonly categories = Object.keys(categoryLabels) as ProductCategory[];

  // Mismas reglas que ProductRequest en el backend. Ver product-validators.ts.
  protected readonly form = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    brand: ['', [Validators.required, Validators.maxLength(100)]],
    model: ['', [Validators.required, Validators.maxLength(200)]],
    category: this.fb.control<ProductCategory | ''>('', Validators.required),
    price: this.fb.control<number | null>(null, [
      Validators.required,
      Validators.min(0.01),
      Validators.max(100_000),
    ]),
    stock: this.fb.control<number | null>(null, [
      Validators.required,
      Validators.min(0),
      Validators.max(100_000),
      integer,
    ]),
    releaseDate: ['', [Validators.required, notMoreThanOneYearAhead]],
    ramGb: this.fb.control<number | null>(null, [Validators.min(1), Validators.max(2048), integer]),
    storageGb: this.fb.control<number | null>(null, [
      Validators.min(1),
      Validators.max(100_000),
      integer,
    ]),
    screenInches: this.fb.control<number | null>(null, [Validators.min(1), Validators.max(99.9)]),
  });

  // Los inputs ya tienen valor en ngOnInit; en el constructor todavía no.
  ngOnInit(): void {
    const id = this.id();
    if (id === undefined) {
      return;
    }

    this.loading.set(true);
    this.productService
      .getById(Number(id))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (product) => {
          // patchValue ignora las propiedades que no son campos del formulario (id).
          this.form.patchValue(product);
          this.loading.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.loadError.set(
            error.status === 404 ? 'El producto no existe.' : 'No se pudo cargar el producto.',
          );
          this.loading.set(false);
        },
      });
  }

  protected save(): void {
    this.submitted.set(true);
    if (this.form.invalid) {
      return;
    }

    this.saving.set(true);
    this.saveErrors.set([]);

    // Con el formulario válido, category ya no puede ser ''.
    const request = this.form.getRawValue() as ProductRequest;
    const id = this.id();
    const save$ =
      id === undefined
        ? this.productService.create(request)
        : this.productService.update(Number(id), request);

    save$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => this.router.navigate(['/products']),
      error: (error: HttpErrorResponse) => {
        this.saveErrors.set(saveErrorMessages(error));
        this.saving.set(false);
      },
    });
  }

  // Mensaje del primer error de un campo, o null si no hay que enseñar ninguno.
  protected errorFor(control: AbstractControl): string | null {
    const errors = control.errors;
    if (!errors || !(control.touched || this.submitted())) {
      return null;
    }
    if (errors['required']) return 'Este campo es obligatorio.';
    if (errors['maxlength']) return `Máximo ${errors['maxlength'].requiredLength} caracteres.`;
    if (errors['min']) return `El valor mínimo es ${errors['min'].min}.`;
    if (errors['max']) return `El valor máximo es ${errors['max'].max}.`;
    if (errors['integer']) return 'Tiene que ser un número entero.';
    if (errors['tooFarAhead']) return 'No puede ser más de un año en el futuro.';
    return 'Valor no válido.';
  }
}

// Convierte la respuesta de error de la API en mensajes para el usuario.
function saveErrorMessages(error: HttpErrorResponse): string[] {
  const messages = validationMessages(error);
  if (messages.length > 0) {
    return messages;
  }
  if (error.status === 404) {
    return ['El producto ya no existe. Puede que alguien lo haya borrado.'];
  }
  return ['No se pudo guardar el producto. Inténtalo de nuevo.'];
}
