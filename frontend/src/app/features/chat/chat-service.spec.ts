import { TestBed } from '@angular/core/testing';
import { firstValueFrom, toArray } from 'rxjs';
import { ChatStreamEvent } from './chat';
import { ChatService } from './chat-service';

describe('ChatService', () => {
  let service: ChatService;

  beforeEach(() => {
    service = TestBed.inject(ChatService);
  });

  afterEach(() => vi.unstubAllGlobals());

  // Una respuesta de fetch cuyo cuerpo llega en los trozos indicados, como en la red.
  function streamedResponse(chunks: string[]): Response {
    const body = new ReadableStream<Uint8Array>({
      start(controller) {
        const encoder = new TextEncoder();
        chunks.forEach((chunk) => controller.enqueue(encoder.encode(chunk)));
        controller.close();
      },
    });
    return new Response(body, { headers: { 'Content-Type': 'text/event-stream' } });
  }

  it('hace POST a /api/chat/stream y emite los eventos según llegan', async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(
        streamedResponse([
          'event: sources\ndata: {"citations":[]}\n\nevent: delta\nda',
          'ta: {"text":"Hola "}\n\nevent: delta\ndata: {"text":"[1]"}\n\n',
          'event: done\ndata: {"elapsedMilliseconds":1200}\n\n',
        ]),
      );
    vi.stubGlobal('fetch', fetchMock);

    const events = await firstValueFrom(
      service.stream({ question: '¿Garantía?', productId: null }).pipe(toArray()),
    );

    expect(events.map((e: ChatStreamEvent) => e.type)).toEqual([
      'sources',
      'delta',
      'delta',
      'done',
    ]);
    expect(events[1]).toEqual({ type: 'delta', text: 'Hola ' });
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('/api/chat/stream');
    expect(init.method).toBe('POST');
    expect(JSON.parse(init.body)).toEqual({ question: '¿Garantía?', productId: null });
  });

  it('si la API responde con error, el Observable falla', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('{}', { status: 400 })));

    await expect(
      firstValueFrom(service.stream({ question: 'ab', productId: null })),
    ).rejects.toThrow('HTTP 400');
  });

  it('al cancelar la suscripción aborta la petición', () => {
    let signal: AbortSignal | undefined;
    vi.stubGlobal(
      'fetch',
      vi.fn((_url: string, init: RequestInit) => {
        signal = init.signal ?? undefined;
        return new Promise(() => {});
      }),
    );

    const subscription = service.stream({ question: '¿Garantía?', productId: null }).subscribe();
    subscription.unsubscribe();

    expect(signal?.aborted).toBe(true);
  });
});
