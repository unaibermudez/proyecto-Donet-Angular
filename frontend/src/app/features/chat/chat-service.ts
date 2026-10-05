import { Service } from '@angular/core';
import { Observable } from 'rxjs';
import { AskRequest, ChatStreamEvent } from './chat';
import { SseParser } from './sse-parser';

// Chat con el asistente. Usa fetch en vez de HttpClient porque hay que leer la respuesta
// a trozos según llega (ReadableStream), y HttpClient entrega la respuesta entera.
// Se envuelve en un Observable para usarlo igual que el resto de servicios: al cancelar
// la suscripción se aborta la petición, y el servidor deja de generar.
@Service()
export class ChatService {
  stream(request: AskRequest): Observable<ChatStreamEvent> {
    return new Observable<ChatStreamEvent>((subscriber) => {
      const controller = new AbortController();

      const read = async () => {
        const response = await fetch('/api/chat/stream', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', Accept: 'text/event-stream' },
          body: JSON.stringify(request),
          signal: controller.signal,
        });
        if (!response.ok || !response.body) {
          throw new Error(`HTTP ${response.status}`);
        }

        const reader = response.body.pipeThrough(new TextDecoderStream()).getReader();
        const parser = new SseParser();
        while (true) {
          const { value, done } = await reader.read();
          if (done) {
            break;
          }
          for (const message of parser.push(value)) {
            subscriber.next({
              type: message.event,
              ...JSON.parse(message.data),
            } as ChatStreamEvent);
          }
        }
        subscriber.complete();
      };

      read().catch((error: unknown) => {
        // Si se abortó porque el usuario canceló, no es un error.
        if (!controller.signal.aborted) {
          subscriber.error(error);
        }
      });

      return () => controller.abort();
    });
  }
}
