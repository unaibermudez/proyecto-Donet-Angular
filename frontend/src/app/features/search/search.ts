// Lo que se envía a POST /api/search. Refleja SearchRequest del backend.
export interface SearchRequest {
  question: string;
  topK: number;
  productId: number | null;
}

// Refleja SearchResponse y SearchResult del backend.
export interface SearchResponse {
  question: string;
  results: SearchResult[];
  elapsedMilliseconds: number;
}

export interface SearchResult {
  chunkId: number;
  documentId: number;
  fileName: string;
  productId: number | null;
  productName: string | null;
  chunkIndex: number;
  content: string;
  // Similitud coseno: 1 = mismo significado, 0 = nada que ver.
  similarity: number;
}
