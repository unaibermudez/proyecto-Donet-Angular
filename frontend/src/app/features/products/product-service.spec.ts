import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Product, ProductRequest } from './product';
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

  const request: ProductRequest = {
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
  };
  const product: Product = { id: 1, ...request };

  it('getAll pide GET /api/products y devuelve la lista', () => {
    const products = [product];
    let received: Product[] | undefined;

    service.getAll().subscribe((result) => (received = result));

    const req = httpMock.expectOne('/api/products');
    expect(req.request.method).toBe('GET');
    expect(received).toBeUndefined(); // aún no ha llegado la respuesta

    req.flush(products);

    expect(received).toEqual(products);
  });

  it('getById pide GET /api/products/{id}', () => {
    let received: Product | undefined;

    service.getById(1).subscribe((result) => (received = result));

    const req = httpMock.expectOne('/api/products/1');
    expect(req.request.method).toBe('GET');
    req.flush(product);
    expect(received).toEqual(product);
  });

  it('create hace POST con el producto en el cuerpo', () => {
    let received: Product | undefined;

    service.create(request).subscribe((result) => (received = result));

    const req = httpMock.expectOne('/api/products');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush(product, { status: 201, statusText: 'Created' });
    expect(received).toEqual(product);
  });

  it('update hace PUT a /api/products/{id} con el producto en el cuerpo', () => {
    service.update(1, request).subscribe();

    const req = httpMock.expectOne('/api/products/1');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual(request);
    req.flush(product);
  });

  it('delete hace DELETE a /api/products/{id}', () => {
    let completed = false;

    service.delete(1).subscribe({ complete: () => (completed = true) });

    const req = httpMock.expectOne('/api/products/1');
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });
    expect(completed).toBe(true);
  });
});
