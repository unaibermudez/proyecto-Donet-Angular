import { CurrencyPipe } from '@angular/common';
import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { categoryLabels, Product } from '../product';
import { ProductService } from '../product-service';

@Component({
  imports: [CurrencyPipe, RouterLink],
  selector: 'app-product-list',
  styleUrl: './product-list.css',
  templateUrl: './product-list.html',
})
export class ProductList {
  private readonly productService = inject(ProductService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly products = signal<Product[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly deletingId = signal<number | null>(null);
  protected readonly deleteError = signal<string | null>(null);
  protected readonly productCount = computed(() => this.products().length);
  protected readonly categoryLabels = categoryLabels;

  constructor() {
    this.productService
      .getAll()
      .pipe(takeUntilDestroyed())
      .subscribe({
        next: (products) => {
          this.products.set(products);
          this.loading.set(false);
        },
        error: () => {
          this.error.set('No se pudieron cargar los productos. ¿Está arrancada la API?');
          this.loading.set(false);
        },
      });
  }

  protected deleteProduct(product: Product): void {
    if (!confirm(`¿Seguro que quieres borrar «${product.name}»? No se puede deshacer.`)) {
      return;
    }

    this.deletingId.set(product.id);
    this.deleteError.set(null);
    // Fuera del constructor, takeUntilDestroyed necesita que le pasen el DestroyRef.
    this.productService
      .delete(product.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          // Se quita de la lista sin volver a pedirla entera a la API.
          this.products.update((products) => products.filter((p) => p.id !== product.id));
          this.deletingId.set(null);
        },
        error: () => {
          this.deleteError.set(`No se pudo borrar «${product.name}».`);
          this.deletingId.set(null);
        },
      });
  }
}
