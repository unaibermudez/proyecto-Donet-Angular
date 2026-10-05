// Lo que se envía a /api/chat/stream. Refleja AskRequest del backend.
export interface AskRequest {
  question: string;
  productId: number | null;
}

// Un fragmento que el modelo recibió como contexto. number es el [n] de la respuesta.
export interface Citation {
  number: number;
  documentId: number;
  fileName: string;
  productName: string | null;
  chunkIndex: number;
  content: string;
  similarity: number;
}

// Eventos del streaming, en este orden: sources → delta (muchos) → done. O error.
export type ChatStreamEvent =
  | { type: 'sources'; citations: Citation[] }
  | { type: 'delta'; text: string }
  | { type: 'done'; elapsedMilliseconds: number }
  | { type: 'error'; message: string };

// Un trozo de la respuesta: texto normal o una cita [n].
export type AnswerSegment = { kind: 'text'; text: string } | { kind: 'citation'; number: number };

// Separa las citas del texto: "Dura 3 años [1][2]." → texto, [1], [2], texto.
// También entiende "[1, 2]", que los modelos escriben a veces.
export function splitCitations(answer: string): AnswerSegment[] {
  const segments: AnswerSegment[] = [];
  let last = 0;
  for (const match of answer.matchAll(/\[(\d+(?:\s*,\s*\d+)*)\]/g)) {
    if (match.index > last) {
      segments.push({ kind: 'text', text: answer.slice(last, match.index) });
    }
    for (const number of match[1].split(',')) {
      segments.push({ kind: 'citation', number: Number(number.trim()) });
    }
    last = match.index + match[0].length;
  }
  if (last < answer.length) {
    segments.push({ kind: 'text', text: answer.slice(last) });
  }
  return segments;
}

// Números de las fuentes que la respuesta cita de verdad.
export function citedNumbers(answer: string): Set<number> {
  return new Set(
    splitCitations(answer)
      .filter((s) => s.kind === 'citation')
      .map((s) => (s as { number: number }).number),
  );
}
