import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import type { Mock } from 'vitest';
import { ProductService } from '../../products/product-service';
import { DocumentService } from '../document-service';
import { UploadedDocument } from '../uploaded-document';
import { DocumentUpload } from './document-upload';

describe('DocumentUpload', () => {
  let fixture: ComponentFixture<DocumentUpload>;
  let element: HTMLElement;
  let documentService: { upload: Mock };
  let uploaded: UploadedDocument[];

  const document: UploadedDocument = {
    id: 1,
    fileName: 'manual.md',
    contentType: 'text/markdown',
    sizeBytes: 8,
    uploadedAt: '2026-10-05T16:00:00Z',
    productId: 3,
    productName: 'Galaxy S25',
    status: 'Pending',
    statusMessage: null,
  };

  beforeEach(async () => {
    documentService = { upload: vi.fn() };
    const productService = {
      getAll: vi.fn().mockReturnValue(of([{ id: 3, name: 'Galaxy S25' }])),
    };

    await TestBed.configureTestingModule({
      imports: [DocumentUpload],
      providers: [
        { provide: DocumentService, useValue: documentService },
        { provide: ProductService, useValue: productService },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(DocumentUpload);
    element = fixture.nativeElement as HTMLElement;
    // Se escucha el output() como lo haría el componente padre.
    uploaded = [];
    fixture.componentInstance.uploaded.subscribe((d) => uploaded.push(d));
    await fixture.whenStable();
  });

  // jsdom no deja escribir en input.files, así que se redefine la propiedad.
  async function chooseFile(file: File): Promise<void> {
    const input = element.querySelector<HTMLInputElement>('#file')!;
    Object.defineProperty(input, 'files', { value: [file], configurable: true });
    input.dispatchEvent(new Event('change'));
    await fixture.whenStable();
  }

  async function chooseProduct(value: string): Promise<void> {
    const select = element.querySelector<HTMLSelectElement>('#product')!;
    select.value = value;
    select.dispatchEvent(new Event('change'));
    await fixture.whenStable();
  }

  async function submit(): Promise<void> {
    element.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    await fixture.whenStable();
  }

  it('rechaza un .txt sin llamar a la API', async () => {
    await chooseFile(new File(['hola'], 'notas.txt'));

    await submit();

    expect(element.querySelector('#file-error')!.textContent).toContain(
      'Solo se pueden subir ficheros .md y .pdf.',
    );
    expect(documentService.upload).not.toHaveBeenCalled();
  });

  it('pide elegir un fichero si se pulsa Subir sin ninguno', async () => {
    await submit();

    expect(element.querySelector('#file-error')!.textContent).toContain('Elige un fichero.');
    expect(documentService.upload).not.toHaveBeenCalled();
  });

  it('sube el fichero con el producto elegido y avisa al padre', async () => {
    documentService.upload.mockReturnValue(of(document));
    const file = new File(['# Manual'], 'manual.md');
    await chooseFile(file);
    await chooseProduct('3');

    await submit();

    expect(documentService.upload).toHaveBeenCalledWith(file, 3);
    expect(uploaded).toEqual([document]);
  });

  it('enseña los errores de validación que devuelve la API', async () => {
    const apiMessage = 'The file is not a valid PDF.';
    documentService.upload.mockReturnValue(
      throwError(
        () => new HttpErrorResponse({ status: 400, error: { errors: { file: [apiMessage] } } }),
      ),
    );
    await chooseFile(new File(['no soy un pdf'], 'falso.pdf'));

    await submit();

    expect(element.querySelector('[role="alert"]')!.textContent).toContain(apiMessage);
    expect(uploaded).toEqual([]);
  });
});
