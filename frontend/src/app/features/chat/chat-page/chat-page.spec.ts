import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, Subject } from 'rxjs';
import type { Mock } from 'vitest';
import { ProductService } from '../../products/product-service';
import { ChatStreamEvent, Citation } from '../chat';
import { ChatService } from '../chat-service';
import { ChatPage } from './chat-page';

describe('ChatPage', () => {
  let fixture: ComponentFixture<ChatPage>;
  let element: HTMLElement;
  let chatService: { stream: Mock };
  let events: Subject<ChatStreamEvent>;

  const citations: Citation[] = [
    {
      number: 1,
      documentId: 14,
      fileName: 'manual-microsoft-xbox-series-s.md',
      productName: 'Microsoft Xbox Series S 512 GB',
      chunkIndex: 0,
      content: 'La Xbox Series S no tiene lector de discos.',
      similarity: 0.79,
    },
    {
      number: 2,
      documentId: 3,
      fileName: 'devoluciones.md',
      productName: null,
      chunkIndex: 1,
      content: 'Los juegos digitales no se pueden devolver.',
      similarity: 0.55,
    },
  ];

  beforeEach(async () => {
    // Un Subject permite emitir los eventos del streaming a mano, uno a uno.
    events = new Subject<ChatStreamEvent>();
    chatService = { stream: vi.fn().mockReturnValue(events) };

    await TestBed.configureTestingModule({
      imports: [ChatPage],
      providers: [
        { provide: ChatService, useValue: chatService },
        { provide: ProductService, useValue: { getAll: vi.fn().mockReturnValue(of([])) } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ChatPage);
    element = fixture.nativeElement as HTMLElement;
    await fixture.whenStable();
  });

  async function ask(question: string): Promise<void> {
    const input = element.querySelector<HTMLInputElement>('#question')!;
    input.value = question;
    input.dispatchEvent(new Event('input'));
    await fixture.whenStable();
    element.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    await fixture.whenStable();
  }

  async function emit(event: ChatStreamEvent): Promise<void> {
    events.next(event);
    await fixture.whenStable();
  }

  it('enseña la respuesta según llega, con las citas y sus fuentes', async () => {
    await ask('¿La Xbox Series S tiene lector de discos?');
    expect(chatService.stream).toHaveBeenCalledWith({
      question: '¿La Xbox Series S tiene lector de discos?',
      productId: null,
    });
    expect(element.textContent).toContain('Buscando en los documentos');

    await emit({ type: 'sources', citations });
    expect(element.textContent).toContain('Pensando');

    await emit({ type: 'delta', text: 'No, no tiene ' });
    await emit({ type: 'delta', text: 'lector de discos [1].' });
    await emit({ type: 'done', elapsedMilliseconds: 12400 });

    expect(element.querySelector('.text')!.textContent).toContain('No, no tiene lector de discos');
    const link = element.querySelector<HTMLAnchorElement>('a.citation')!;
    expect(link.getAttribute('href')).toBe('#source-1-1');
    expect(element.querySelectorAll('.sources li').length).toBe(2);
    expect(element.querySelector('#source-1-1')!.classList).toContain('cited');
    expect(element.querySelector('#source-1-2')!.classList).not.toContain('cited');
    expect(element.textContent).toContain('Respondido en 12,4 s');
  });

  it('una cita a un fragmento que no existe se enseña sin enlace', async () => {
    await ask('¿Qué móvil admite tarjeta microSD?');
    await emit({ type: 'sources', citations });

    await emit({ type: 'delta', text: 'No admite tarjetas microSD [5].' });

    expect(element.querySelector('a.citation')).toBeNull();
    expect(element.querySelector('.citation-invalid')!.textContent).toContain('[5]');
  });

  it('mientras responde se puede detener', async () => {
    await ask('¿Cuánto dura la garantía?');
    await emit({ type: 'sources', citations });

    element.querySelector<HTMLButtonElement>('button[type="button"]:not(.example)')!.click();
    await fixture.whenStable();

    expect(events.observed).toBe(false);
    expect(element.textContent).toContain('Respuesta cancelada');
    expect(element.querySelector('button[type="submit"]')).not.toBeNull();
  });

  it('si el streaming falla, lo dice', async () => {
    await ask('¿Cuánto dura la garantía?');

    await emit({
      type: 'error',
      message: 'No se pudo generar la respuesta. ¿Está arrancado Ollama?',
    });

    expect(element.querySelector('[role="alert"]')!.textContent).toContain('Ollama');
  });
});
