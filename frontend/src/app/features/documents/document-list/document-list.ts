import { DatePipe } from '@angular/common';
import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DocumentUpload } from '../document-upload/document-upload';
import { DocumentService } from '../document-service';
import { FileSizePipe } from '../file-size-pipe';
import { isInProgress, statusLabels, UploadedDocument } from '../uploaded-document';

// Cada cuánto se vuelve a pedir la lista mientras haya documentos procesándose.
export const refreshIntervalMs = 2000;

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
  protected readonly actionError = signal<string | null>(null);
  protected readonly documentCount = computed(() => this.documents().length);
  protected readonly readyCount = computed(
    () => this.documents().filter((d) => d.status === 'Ready').length,
  );
  protected readonly statusLabels = statusLabels;
  protected readonly isInProgress = isInProgress;

  private refreshTimer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    this.destroyRef.onDestroy(() => clearTimeout(this.refreshTimer));
    this.load();
  }

  // Lo llama el evento (uploaded) del hijo. El más reciente va primero, como en la API.
  protected onUploaded(document: UploadedDocument): void {
    this.documents.update((documents) => [document, ...documents]);
    this.scheduleRefresh();
  }

  protected reprocess(document: UploadedDocument): void {
    this.actionError.set(null);
    this.documentService
      .ingest(document.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.replace(updated);
          this.scheduleRefresh();
        },
        error: () => this.actionError.set(`No se pudo reprocesar «${document.fileName}».`),
      });
  }

  protected deleteDocument(document: UploadedDocument): void {
    if (!confirm(`¿Seguro que quieres borrar «${document.fileName}»? No se puede deshacer.`)) {
      return;
    }

    this.deletingId.set(document.id);
    this.actionError.set(null);
    this.documentService
      .delete(document.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.documents.update((documents) => documents.filter((d) => d.id !== document.id));
          this.deletingId.set(null);
        },
        error: () => {
          this.actionError.set(`No se pudo borrar «${document.fileName}».`);
          this.deletingId.set(null);
        },
      });
  }

  private load(): void {
    this.documentService
      .getAll()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (documents) => {
          this.documents.set(documents);
          this.loading.set(false);
          this.scheduleRefresh();
        },
        error: () => {
          this.error.set('No se pudieron cargar los documentos. ¿Está arrancada la API?');
          this.loading.set(false);
        },
      });
  }

  // La ingesta va en segundo plano en el servidor. Mientras algún documento no haya
  // terminado, se vuelve a pedir la lista cada pocos segundos (polling).
  private scheduleRefresh(): void {
    clearTimeout(this.refreshTimer);
    if (this.documents().some((d) => isInProgress(d.status))) {
      this.refreshTimer = setTimeout(() => this.load(), refreshIntervalMs);
    }
  }

  private replace(updated: UploadedDocument): void {
    this.documents.update((documents) => documents.map((d) => (d.id === updated.id ? updated : d)));
  }
}
