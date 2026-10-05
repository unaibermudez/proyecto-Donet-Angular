import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Product } from './product';
import { ProductService } from './product-service';

describe('ProductService', () => {
  let service: ProductService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      // provideHttpClientTesting sustituye la red por un backend falso que controla el test.
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(ProductService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  // Falla si queda alguna petición sin responder o alguna que nadie esperaba.
  afterEach(() => httpMock.verify());

  it('getAll pide GET /api/products y devuelve la lista', () => {
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
    ];
    let received: Product[] | undefined;

    service.getAll().subscribe((result) => (received = result));

    const req = httpMock.expectOne('/api/products');
    expect(req.request.method).toBe('GET');
    expect(received).toBeUndefined(); // aún no ha llegado la respuesta

    req.flush(products);

    expect(received).toEqual(products);
  });
});
