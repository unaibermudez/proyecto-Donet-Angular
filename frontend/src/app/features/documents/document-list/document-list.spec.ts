import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import type { Mock } from 'vitest';
import { ProductService } from '../../products/product-service';
import { DocumentService } from '../document-service';
import { UploadedDocument } from '../uploaded-document';
import { DocumentList } from './document-list';

describe('DocumentList', () => {
  let fixture: ComponentFixture<DocumentList>;
  let element: HTMLElement;
  let documentService: { getAll: Mock; delete: Mock; upload: Mock; contentUrl: Mock };

  const documents: UploadedDocument[] = [
    {
      id: 1,
      fileName: 'manual-galaxy.pdf',
      contentType: 'application/pdf',
      sizeBytes: 2048,
      uploadedAt: '2026-10-05T16:00:00Z',
      productId: 3,
      productName: 'Galaxy S25',
    },
    {
      id: 2,
      fileName: 'garantia.md',
      contentType: 'text/markdown',
      sizeBytes: 300,
      uploadedAt: '2026-10-04T10:00:00Z',
      productId: null,
      productName: null,
    },
  ];

  beforeEach(async () => {
    documentService = {
      getAll: vi.fn().mockReturnValue(of(documents)),
      delete: vi.fn().mockReturnValue(of(undefined)),
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

    fixture = TestBed.createComponent(DocumentList);
    element = fixture.nativeElement as HTMLElement;
    await fixture.whenStable();
  });

  afterEach(() => vi.restoreAllMocks());

  function rows(): NodeListOf<HTMLTableRowElement> {
    return element.querySelectorAll('tbody tr');
  }

  it('pinta cada documento con su producto, o "General" si no tiene', () => {
    expect(rows().length).toBe(2);
    expect(rows()[0].textContent).toContain('Galaxy S25');
    expect(rows()[1].textContent).toContain('General');
    expect(rows()[0].querySelector('a')!.getAttribute('href')).toBe('/api/documents/1/content');
  });

  it('borra el documento si el usuario confirma', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    element.querySelector<HTMLButtonElement>('button[aria-label="Borrar garantia.md"]')!.click();
    await fixture.whenStable();

    expect(documentService.delete).toHaveBeenCalledWith(2);
    expect(rows().length).toBe(1);
  });
});
