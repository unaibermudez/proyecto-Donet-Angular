import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import type { Mock } from 'vitest';
import { ProductService } from '../../products/product-service';
import { DocumentService } from '../document-service';
import { UploadedDocument } from '../uploaded-document';
import { DocumentList, refreshIntervalMs } from './document-list';

describe('DocumentList', () => {
  let fixture: ComponentFixture<DocumentList>;
  let element: HTMLElement;
  let documentService: {
    getAll: Mock;
    delete: Mock;
    ingest: Mock;
    upload: Mock;
    contentUrl: Mock;
  };

  const manual: UploadedDocument = {
    id: 1,
    fileName: 'manual-galaxy.pdf',
    contentType: 'application/pdf',
    sizeBytes: 2048,
    uploadedAt: '2026-10-05T16:00:00Z',
    productId: 3,
    productName: 'Galaxy S25',
    status: 'Ready',
    statusMessage: null,
  };
  const warranty: UploadedDocument = {
    id: 2,
    fileName: 'garantia.md',
    contentType: 'text/markdown',
    sizeBytes: 300,
    uploadedAt: '2026-10-04T10:00:00Z',
    productId: null,
    productName: null,
    status: 'Failed',
    statusMessage: 'The document has no text.',
  };

  beforeEach(async () => {
    documentService = {
      getAll: vi.fn().mockReturnValue(of([manual, warranty])),
      delete: vi.fn().mockReturnValue(of(undefined)),
      ingest: vi.fn(),
      upload: vi.fn(),
      contentUrl: vi.fn((id: number) => `/api/documents/${id}/content`),
    };

    await TestBed.configureTestingModule({
      imports: [DocumentList],
      providers: [
        { provide: DocumentService, useValue: documentService },
        // El formulario hijo carga los productos para el desplegable.
        { provide: ProductService, useValue: { getAll: vi.fn().mockReturnValue(of([])) } },
      ],
    }).compileComponents();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.useRealTimers();
  });

  async function render(): Promise<void> {
    fixture = TestBed.createComponent(DocumentList);
    element = fixture.nativeElement as HTMLElement;
    await fixture.whenStable();
  }

  function rows(): NodeListOf<HTMLTableRowElement> {
    return element.querySelectorAll('tbody tr');
  }

  it('pinta cada documento con su producto o "General", y su estado', async () => {
    await render();

    expect(rows().length).toBe(2);
    expect(rows()[0].textContent).toContain('Galaxy S25');
    expect(rows()[0].textContent).toContain('Listo');
    expect(rows()[1].textContent).toContain('General');
    expect(rows()[1].textContent).toContain('Error');
    expect(rows()[1].textContent).toContain('The document has no text.');
    expect(rows()[0].querySelector('a')!.getAttribute('href')).toBe('/api/documents/1/content');
  });

  it('borra el documento si el usuario confirma', async () => {
    await render();
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    element.querySelector<HTMLButtonElement>('button[aria-label="Borrar garantia.md"]')!.click();
    await fixture.whenStable();

    expect(documentService.delete).toHaveBeenCalledWith(2);
    expect(rows().length).toBe(1);
  });

  it('reprocesa un documento y lo enseña como pendiente', async () => {
    documentService.ingest.mockReturnValue(
      of({ ...warranty, status: 'Pending', statusMessage: null }),
    );
    await render();

    element
      .querySelector<HTMLButtonElement>('button[aria-label="Reprocesar garantia.md"]')!
      .click();
    await fixture.whenStable();

    expect(documentService.ingest).toHaveBeenCalledWith(2);
    expect(rows()[1].textContent).toContain('Pendiente');
    expect(
      element.querySelector<HTMLButtonElement>('button[aria-label="Reprocesar garantia.md"]')!
        .disabled,
    ).toBe(true);
  });

  it('vuelve a pedir la lista mientras haya documentos procesándose', () => {
    vi.useFakeTimers();
    documentService.getAll
      .mockReturnValueOnce(of([{ ...manual, status: 'Processing' }]))
      .mockReturnValueOnce(of([manual]));

    // Sin whenStable: con los temporizadores falsos se avanza el tiempo a mano.
    TestBed.createComponent(DocumentList);
    expect(documentService.getAll).toHaveBeenCalledTimes(1);

    vi.advanceTimersByTime(refreshIntervalMs);
    expect(documentService.getAll).toHaveBeenCalledTimes(2);

    // Ya está todo listo: no se programa otra consulta.
    vi.advanceTimersByTime(refreshIntervalMs * 3);
    expect(documentService.getAll).toHaveBeenCalledTimes(2);
  });
});
