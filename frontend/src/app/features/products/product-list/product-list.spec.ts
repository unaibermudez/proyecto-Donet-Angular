import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import type { Mock } from 'vitest';
import { Product } from '../product';
import { ProductService } from '../product-service';
import { ProductList } from './product-list';

describe('ProductList', () => {
  let fixture: ComponentFixture<ProductList>;
  let element: HTMLElement;
  // Servicio falso: los tests del componente no hacen HTTP, solo comprueban qué se le pide.
  let productService: { getAll: Mock; delete: Mock };

  const products: Product[] = [
    {
      id: 1,
      name: 'Galaxy S25',
      brand: 'Samsung',
      model: 'SM-S931B',
      category: 'Phone',
      price: 899.99,
      stock: 12,
      releaseDate: '2025-02-07',
      ramGb: 12,
      storageGb: 256,
      screenInches: 6.2,
    },
    {
      id: 2,
      name: 'PlayStation 5',
      brand: 'Sony',
      model: 'CFI-2016',
      category: 'Console',
      price: 549,
      stock: 5,
      releaseDate: '2020-11-19',
      ramGb: 16,
      storageGb: 1024,
      screenInches: null,
    },
  ];

  beforeEach(async () => {
    productService = {
      getAll: vi.fn().mockReturnValue(of(products)),
      delete: vi.fn().mockReturnValue(of(undefined)),
    };

    await TestBed.configureTestingModule({
      imports: [ProductList],
      providers: [provideRouter([]), { provide: ProductService, useValue: productService }],
    }).compileComponents();

    fixture = TestBed.createComponent(ProductList);
    element = fixture.nativeElement as HTMLElement;
    await fixture.whenStable();
  });

  afterEach(() => vi.restoreAllMocks());

  function rows(): NodeListOf<HTMLTableRowElement> {
    return element.querySelectorAll('tbody tr');
  }

  async function clickDelete(productName: string): Promise<void> {
    element.querySelector<HTMLButtonElement>(`button[aria-label="Borrar ${productName}"]`)!.click();
    await fixture.whenStable();
  }

  it('pinta una fila por producto', () => {
    expect(rows().length).toBe(2);
    expect(rows()[0].textContent).toContain('Galaxy S25');
    expect(rows()[1].textContent).toContain('Consola');
  });

  it('borra el producto si el usuario confirma', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    await clickDelete('Galaxy S25');

    expect(productService.delete).toHaveBeenCalledWith(1);
    expect(rows().length).toBe(1);
    expect(rows()[0].textContent).toContain('PlayStation 5');
  });

  it('no borra nada si el usuario cancela', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(false);

    await clickDelete('Galaxy S25');

    expect(productService.delete).not.toHaveBeenCalled();
    expect(rows().length).toBe(2);
  });
});
