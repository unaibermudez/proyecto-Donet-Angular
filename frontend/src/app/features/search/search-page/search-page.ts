import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { validationMessages } from '../../../core/api-errors';
import { Product } from '../../products/product';
import { ProductService } from '../../products/product-service';
import { SearchResponse } from '../search';
import { SearchService } from '../search-service';

// Ejemplos para probar sin pensar una pregunta.
const exampleQuestions = [
  '¿La Xbox Series S tiene lector de discos?',
  '¿Cuántos días tengo para devolver un producto?',
  '¿Qué hago si el mando se mueve solo?',
  '¿Se puede ampliar la memoria del portátil?',
];

// Página /search: escribe una pregunta y ve qué fragmentos encuentra la búsqueda
// semántica, con su similitud. Sirve para comprobar la "R" del RAG antes del chat.
@Component({
  imports: [ReactiveFormsModule],
  selector: 'app-search-page',
  styleUrl: './search-page.css',
  templateUrl: './search-page.html',
})
export class SearchPage {
  private readonly searchService = inject(SearchService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly products = signal<Product[]>([]);
  protected readonly searching = signal(false);
  protected readonly response = signal<SearchResponse | null>(null);
  protected readonly errors = signal<string[]>([]);
  protected readonly submitted = signal(false);
  protected readonly exampleQuestions = exampleQuestions;
  protected readonly topKOptions = [3, 5, 10];

  // Mismas reglas que SearchRequest en el backend.
  protected readonly form = this.fb.group({
    question: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(500)]],
    topK: this.fb.control(5),
    productId: this.fb.control<number | null>(null),
  });

  constructor() {
    inject(ProductService)
      .getAll()
      .pipe(takeUntilDestroyed())
      .subscribe({
        next: (products) => this.products.set(products),
        error: () => this.products.set([]),
      });
  }

  protected useExample(question: string): void {
    this.form.controls.question.setValue(question);
    this.search();
  }

  protected search(): void {
    this.submitted.set(true);
    if (this.form.invalid) {
      return;
    }

    this.searching.set(true);
    this.errors.set([]);
    this.searchService
      .search(this.form.getRawValue())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.response.set(response);
          this.searching.set(false);
        },
        error: (error: HttpErrorResponse) => {
          const messages = validationMessages(error);
          this.errors.set(
            messages.length > 0
              ? messages
              : ['No se pudo buscar. ¿Están arrancados la API y Ollama?'],
          );
          this.searching.set(false);
        },
      });
  }

  // 0,7843 → "78 %"
  protected percentage(similarity: number): string {
    return `${Math.round(similarity * 100)} %`;
  }

  // Ancho de la barra, entre 0 y 100. La similitud coseno puede ser negativa.
  protected barWidth(similarity: number): number {
    return Math.min(100, Math.max(0, Math.round(similarity * 100)));
  }

  protected questionError(): string | null {
    const control = this.form.controls.question;
    if (!control.errors || !(control.touched || this.submitted())) {
      return null;
    }
    if (control.errors['required']) return 'Escribe una pregunta.';
    if (control.errors['minlength']) return 'La pregunta tiene que tener al menos 3 caracteres.';
    return 'La pregunta no puede pasar de 500 caracteres.';
  }
}
