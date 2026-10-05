import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { DocumentService } from './document-service';
import { UploadedDocument } from './uploaded-document';

describe('DocumentService', () => {
  let service: DocumentService;
  let httpMock: HttpTestingController;

  const document: UploadedDocument = {
    id: 1,
    fileName: 'garantia.md',
    contentType: 'text/markdown',
    sizeBytes: 120,
    uploadedAt: '2026-10-05T16:00:00Z',
    productId: null,
    productName: null,
    status: 'Ready',
    statusMessage: null,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(DocumentService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getAll sin producto pide GET /api/documents', () => {
    service.getAll().subscribe();

    const req = httpMock.expectOne('/api/documents');
    expect(req.request.method).toBe('GET');
    req.flush([document]);
  });

  it('getAll con producto añade ?productId=', () => {
    service.getAll(7).subscribe();

    const req = httpMock.expectOne('/api/documents?productId=7');
    expect(req.request.params.get('productId')).toBe('7');
    req.flush([]);
  });

  it('upload envía el fichero y el producto como FormData', () => {
    const file = new File(['# Manual'], 'manual.md', { type: 'text/markdown' });

    service.upload(file, 3).subscribe();

    const req = httpMock.expectOne('/api/documents');
    expect(req.request.method).toBe('POST');
    const body = req.request.body as FormData;
    expect(body.get('file')).toBeInstanceOf(File);
    expect((body.get('file') as File).name).toBe('manual.md');
    expect(body.get('productId')).toBe('3');
    req.flush({ ...document, id: 2, fileName: 'manual.md', productId: 3 });
  });

  it('upload sin producto no envía productId', () => {
    const file = new File(['# Garantía'], 'garantia.md');

    service.upload(file, null).subscribe();

    const req = httpMock.expectOne('/api/documents');
    expect((req.request.body as FormData).has('productId')).toBe(false);
    req.flush(document);
  });

  it('ingest hace POST a /api/documents/{id}/ingest', () => {
    let received: UploadedDocument | undefined;

    service.ingest(1).subscribe((result) => (received = result));

    const req = httpMock.expectOne('/api/documents/1/ingest');
    expect(req.request.method).toBe('POST');
    req.flush({ ...document, status: 'Pending' }, { status: 202, statusText: 'Accepted' });
    expect(received?.status).toBe('Pending');
  });

  it('delete hace DELETE a /api/documents/{id}', () => {
    service.delete(1).subscribe();

    const req = httpMock.expectOne('/api/documents/1');
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });
  });
});
