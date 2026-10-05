import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SearchResponse } from './search';
import { SearchService } from './search-service';

describe('SearchService', () => {
  let service: SearchService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(SearchService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('search hace POST /api/search con la pregunta en el cuerpo', () => {
    const request = { question: '¿Cuánto dura la garantía?', topK: 5, productId: null };
    const response: SearchResponse = {
      question: request.question,
      results: [],
      elapsedMilliseconds: 40,
    };
    let received: SearchResponse | undefined;

    service.search(request).subscribe((result) => (received = result));

    const req = httpMock.expectOne('/api/search');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(request);
    req.flush(response);
    expect(received).toEqual(response);
  });
});
