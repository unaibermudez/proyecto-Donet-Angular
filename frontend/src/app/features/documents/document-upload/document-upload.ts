import { HttpErrorResponse } from '@angular/common/http';
import {
  Component,
  DestroyRef,
  ElementRef,
  inject,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { validationMessages } from '../../../core/api-errors';
import { Product } from '../../products/product';
import { ProductService } from '../../products/product-service';
import { DocumentService } from '../document-service';
import {
  allowedExtensions,
  maxFileSizeMegabytes,
  UploadedDocument,
  validateFile,
} from '../uploaded-document';

// Formulario de subida. No sabe nada de la lista: avisa al padre con el evento
// (uploaded) y es el padre quien decide qué hacer con el documento nuevo.
@Component({
  selector: 'app-document-upload',
  styleUrl: './document-upload.css',
  templateUrl: './document-upload.html',
})
export class DocumentUpload {
  private readonly documentService = inject(DocumentService);
  private readonly destroyRef = inject(DestroyRef);

  // Evento hacia el componente padre. Es la prop onUploaded de React.
  readonly uploaded = output<UploadedDocument>();

  // Referencia al <input type="file" #fileInput>, para vaciarlo tras subir.
  private readonly fileInput = viewChild.required<ElementRef<HTMLInputElement>>('fileInput');

  protected readonly products = signal<Product[]>([]);
  protected readonly selectedFile = signal<File | null>(null);
  protected readonly productId = signal<number | null>(null);
  protected readonly fileError = signal<string | null>(null);
  protected readonly uploading = signal(false);
  protected readonly uploadErrors = signal<string[]>([]);

  protected readonly accept = allowedExtensions.join(',');
  protected readonly maxFileSizeMegabytes = maxFileSizeMegabytes;

  constructor() {
    // Para el desplegable de producto. Si falla, solo se podrán subir documentos generales.
    inject(ProductService)
      .getAll()
      .pipe(takeUntilDestroyed())
      .subscribe({
        next: (products) => this.products.set(products),
        error: () => this.products.set([]),
      });
  }

  // Un <input type="file"> no funciona con formControlName: el fichero se lee del evento.
  protected onFileSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    this.selectedFile.set(file);
    this.fileError.set(file ? validateFile(file) : null);
    this.uploadErrors.set([]);
  }

  protected onProductSelected(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.productId.set(value === '' ? null : Number(value));
  }

  protected upload(event: SubmitEvent): void {
    // Sin esto, el navegador enviaría el formulario y recargaría la página.
    event.preventDefault();

    const file = this.selectedFile();
    if (file === null) {
      this.fileError.set('Elige un fichero.');
      return;
    }
    if (this.fileError()) {
      return;
    }

    this.uploading.set(true);
    this.uploadErrors.set([]);
    this.documentService
      .upload(file, this.productId())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (document) => {
          this.uploaded.emit(document);
          this.selectedFile.set(null);
          this.fileInput().nativeElement.value = '';
          this.uploading.set(false);
        },
        error: (error: HttpErrorResponse) => {
          const messages = validationMessages(error);
          this.uploadErrors.set(
            messages.length > 0 ? messages : ['No se pudo subir el documento. Inténtalo de nuevo.'],
          );
          this.uploading.set(false);
        },
      });
  }
}
