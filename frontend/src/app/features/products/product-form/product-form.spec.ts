import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { Product, ProductRequest } from '../product';
import { ProductService } from '../product-service';
import { ProductForm } from './product-form';

describe('ProductForm', () => {
  let fixture: ComponentFixture<ProductForm>;
  let element: HTMLElement;
  let navigate: Mock;
  // Servicio falso: los tests del componente no hacen HTTP, solo comprueban qué se le pide.
  let productService: { getById: Mock; create: Mock; update: Mock };

  const request: ProductRequest = {
    name: 'Galaxy S25',
    brand: 'Samsung',
    model: 'SM-S931B',
    category: 'Phone',
    price: 899.99,
    stock: 12,
    releaseDate: '2025-02-07',
    ramGb: null,
    storageGb: null,
    screenInches: null,
  };
  const product: Product = { id: 1, ...request };

  beforeEach(async () => {
    productService = { getById: vi.fn(), create: vi.fn(), update: vi.fn() };

    await TestBed.configureTestingModule({
      imports: [ProductForm],
      providers: [provideRouter([]), { provide: ProductService, useValue: productService }],
    }).compileComponents();

    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true) as Mock;
    fixture = TestBed.createComponent(ProductForm);
    element = fixture.nativeElement as HTMLElement;
  });

  // Escribe en un campo como lo haría el usuario: cambia el valor y lanza el evento.
  function fill(id: string, value: string): void {
    const field = element.querySelector<HTMLInputElement | HTMLSelectElement>(`#${id}`)!;
    field.value = value;
    field.dispatchEvent(new Event(field instanceof HTMLSelectElement ? 'change' : 'input'));
  }

  function fillRequiredFields(): void {
    fill('name', 'Galaxy S25');
    fill('brand', 'Samsung');
    fill('model', 'SM-S931B');
    fill('category', 'Phone');
    fill('price', '899.99');
    fill('stock', '12');
    fill('releaseDate', '2025-02-07');
  }

  async function submit(): Promise<void> {
    element.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    await fixture.whenStable();
  }

  it('con el formulario vacío no llama a la API y enseña los errores', async () => {
    await fixture.whenStable();

    await submit();

    expect(productService.create).not.toHaveBeenCalled();
    const errors = element.querySelectorAll('.field-error');
    // Los 7 campos obligatorios; los opcionales vacíos son válidos.
    expect(errors.length).toBe(7);
    expect(errors[0].textContent).toContain('Este campo es obligatorio.');
    expect(element.querySelector('#name')!.getAttribute('aria-invalid')).toBe('true');
  });

  it('valida en el cliente las mismas reglas que el backend', async () => {
    await fixture.whenStable();
    fillRequiredFields();
    fill('stock', '1.5');
    fill('price', '0');

    await submit();

    expect(productService.create).not.toHaveBeenCalled();
    expect(element.querySelector('#price-error')!.textContent).toContain(
      'El valor mínimo es 0.01.',
    );
    expect(element.querySelector('#stock-error')!.textContent).toContain(
      'Tiene que ser un número entero.',
    );
  });

  it('crea el producto con los datos del formulario y vuelve a la lista', async () => {
    productService.create.mockReturnValue(of(product));
    await fixture.whenStable();
    fillRequiredFields();

    await submit();

    expect(productService.create).toHaveBeenCalledWith(request);
    expect(navigate).toHaveBeenCalledWith(['/products']);
  });

  it('enseña los errores de validación que devuelve la API', async () => {
    const apiMessage = 'The release date cannot be more than one year in the future.';
    productService.create.mockReturnValue(
      throwError(
        () =>
          new HttpErrorResponse({ status: 400, error: { errors: { ReleaseDate: [apiMessage] } } }),
      ),
    );
    await fixture.whenStable();
    fillRequiredFields();

    await submit();

    expect(element.querySelector('[role="alert"]')!.textContent).toContain(apiMessage);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('en modo edición carga el producto y lo guarda con update', async () => {
    productService.getById.mockReturnValue(of(product));
    productService.update.mockReturnValue(of(product));
    fixture.componentRef.setInput('id', '1');
    await fixture.whenStable();

    expect(productService.getById).toHaveBeenCalledWith(1);
    expect(element.querySelector('h2')!.textContent).toContain('Editar producto');
    expect(element.querySelector<HTMLInputElement>('#name')!.value).toBe('Galaxy S25');

    fill('stock', '20');
    await submit();

    expect(productService.update).toHaveBeenCalledWith(1, { ...request, stock: 20 });
    expect(navigate).toHaveBeenCalledWith(['/products']);
  });
});
