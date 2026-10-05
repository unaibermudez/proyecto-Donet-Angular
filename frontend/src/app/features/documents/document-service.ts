import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Service } from '@angular/core';
import { Observable } from 'rxjs';
import { UploadedDocument } from './uploaded-document';

// Acceso a la API de documentos.
@Service()
export class DocumentService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/documents';

  // Sin productId devuelve todos; con él, solo los de ese producto.
  getAll(productId?: number): Observable<UploadedDocument[]> {
    const params =
      productId === undefined ? undefined : new HttpParams().set('productId', productId);
    return this.http.get<UploadedDocument[]>(this.baseUrl, { params });
  }

  // Envía el fichero como multipart/form-data. No hay que poner la cabecera
  // Content-Type: el navegador la añade con el "boundary" que separa las partes.
  upload(file: File, productId: number | null): Observable<UploadedDocument> {
    const form = new FormData();
    form.append('file', file);
    if (productId !== null) {
      form.append('productId', String(productId));
    }
    return this.http.post<UploadedDocument>(this.baseUrl, form);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  // Para un enlace de descarga normal (<a href>): lo descarga el navegador, no HttpClient.
  contentUrl(id: number): string {
    return `${this.baseUrl}/${id}/content`;
  }
}
