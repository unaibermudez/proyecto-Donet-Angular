import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { ProductService } from '../../products/product-service';
import { SearchResponse } from '../search';
import { SearchService } from '../search-service';
import { SearchPage } from './search-page';

describe('SearchPage', () => {
  let fixture: ComponentFixture<SearchPage>;
  let element: HTMLElement;
  let searchService: { search: Mock };

  const response: SearchResponse = {
    question: '¿La Xbox Series S tiene lector de discos?',
    elapsedMilliseconds: 85,
    results: [
      {
        chunkId: 40,
        documentId: 14,
        fileName: 'manual-microsoft-xbox-series-s.md',
        productId: 10,
        productName: 'Microsoft Xbox Series S 512 GB',
        chunkIndex: 0,
        content: 'La Xbox Series S no tiene lector de discos.',
        similarity: 0.7853,
      },
      {
        chunkId: 7,
        documentId: 3,
        fileName: 'devoluciones.md',
        productId: null,
        productName: null,
        chunkIndex: 1,
        content: 'Los juegos digitales con código canjeado no se pueden devolver.',
        similarity: 0.6012,
      },
    ],
  };

  beforeEach(async () => {
    searchService = { search: vi.fn() };

    await TestBed.configureTestingModule({
      imports: [SearchPage],
      providers: [
        { provide: SearchService, useValue: searchService },
        {
          provide: ProductService,
          useValue: { getAll: vi.fn().mockReturnValue(of([{ id: 10, name: 'Xbox Series S' }])) },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(SearchPage);
    element = fixture.nativeElement as HTMLElement;
    await fixture.whenStable();
  });

  function type(value: string): void {
    const input = element.querySelector<HTMLInputElement>('#question')!;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  async function submit(): Promise<void> {
    element.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    await fixture.whenStable();
  }

  it('sin pregunta no busca y pide escribir una', async () => {
    await submit();

    expect(searchService.search).not.toHaveBeenCalled();
    expect(element.querySelector('#question-error')!.textContent).toContain(
      'Escribe una pregunta.',
    );
  });

  it('busca con la pregunta, el número de resultados y el producto, y pinta los fragmentos', async () => {
    searchService.search.mockReturnValue(of(response));
    type('¿La Xbox Series S tiene lector de discos?');
    const select = element.querySelector<HTMLSelectElement>('#productId')!;
    select.selectedIndex = 1;
    select.dispatchEvent(new Event('change'));

    await submit();

    expect(searchService.search).toHaveBeenCalledWith({
      question: '¿La Xbox Series S tiene lector de discos?',
      topK: 5,
      productId: 10,
    });
    const results = element.querySelectorAll('.result');
    expect(results.length).toBe(2);
    expect(results[0].textContent).toContain('manual-microsoft-xbox-series-s.md');
    expect(results[0].textContent).toContain('79 %');
    expect(results[1].textContent).toContain('General');
    expect(element.querySelector('h3')!.textContent).toContain('85 ms');
  });

  it('un ejemplo rellena la pregunta y busca', async () => {
    searchService.search.mockReturnValue(of(response));

    element.querySelector<HTMLButtonElement>('.example')!.click();
    await fixture.whenStable();

    expect(searchService.search).toHaveBeenCalledTimes(1);
    expect(element.querySelector<HTMLInputElement>('#question')!.value).toBe(
      '¿La Xbox Series S tiene lector de discos?',
    );
  });

  it('si la API falla, lo dice', async () => {
    searchService.search.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 500 })));
    type('garantía');

    await submit();

    expect(element.querySelector('[role="alert"]')!.textContent).toContain('No se pudo buscar');
  });
});
