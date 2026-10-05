import { DatePipe } from '@angular/common';
import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DocumentUpload } from '../document-upload/document-upload';
import { DocumentService } from '../document-service';
import { FileSizePipe } from '../file-size-pipe';
import { UploadedDocument } from '../uploaded-document';

// Página de documentos: el formulario de subida y la lista.
@Component({
  imports: [DatePipe, DocumentUpload, FileSizePipe],
  selector: 'app-document-list',
  styleUrl: './document-list.css',
  templateUrl: './document-list.html',
})
export class DocumentList {
  protected readonly documentService = inject(DocumentService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly documents = signal<UploadedDocument[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly deletingId = signal<number | null>(null);
  protected readonly deleteError = signal<string | null>(null);
  protected readonly documentCount = computed(() => this.documents().length);

  constructor() {
    this.documentService
      .getAll()
      .pipe(takeUntilDestroyed())
      .subscribe({
        next: (documents) => {
          this.documents.set(documents);
          this.loading.set(false);
        },
        error: () => {
          this.error.set('No se pudieron cargar los documentos. ¿Está arrancada la API?');
          this.loading.set(false);
        },
      });
  }

  // Lo llama el evento (uploaded) del hijo. El más reciente va primero, como en la API.
  protected onUploaded(document: UploadedDocument): void {
    this.documents.update((documents) => [document, ...documents]);
  }

  protected deleteDocument(document: UploadedDocument): void {
    if (!confirm(`¿Seguro que quieres borrar «${document.fileName}»? No se puede deshacer.`)) {
      return;
    }

    this.deletingId.set(document.id);
    this.deleteError.set(null);
    this.documentService
      .delete(document.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.documents.update((documents) => documents.filter((d) => d.id !== document.id));
          this.deletingId.set(null);
        },
        error: () => {
          this.deleteError.set(`No se pudo borrar «${document.fileName}».`);
          this.deletingId.set(null);
        },
      });
  }
}
