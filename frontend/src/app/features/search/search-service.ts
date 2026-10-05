import { HttpClient } from '@angular/common/http';
import { inject, Service } from '@angular/core';
import { Observable } from 'rxjs';
import { SearchRequest, SearchResponse } from './search';

// Búsqueda semántica en los documentos (sin LLM).
@Service()
export class SearchService {
  private readonly http = inject(HttpClient);

  search(request: SearchRequest): Observable<SearchResponse> {
    return this.http.post<SearchResponse>('/api/search', request);
  }
}
