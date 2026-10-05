import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Subscription } from 'rxjs';
import { Product } from '../../products/product';
import { ProductService } from '../../products/product-service';
import { ChatStreamEvent, Citation, citedNumbers, splitCitations } from '../chat';
import { ChatService } from '../chat-service';

// Una pregunta y su respuesta. Las preguntas son independientes: el asistente no
// recuerda las anteriores (ver la documentación del paso 10).
export interface ChatTurn {
  id: number;
  question: string;
  answer: string;
  citations: Citation[];
  status: 'searching' | 'answering' | 'done' | 'error' | 'cancelled';
  error: string | null;
  elapsedMilliseconds: number | null;
}

const exampleQuestions = [
  '¿La Xbox Series S tiene lector de discos?',
  '¿Cuántos días tengo para devolver un producto?',
  '¿Qué hago si el mando de la Switch 2 se mueve solo?',
  '¿Hacéis envíos a Canarias?',
];

// Página /chat: preguntas al asistente, que responde con los documentos de la tienda
// y cita sus fuentes. La respuesta llega poco a poco (streaming).
@Component({
  imports: [ReactiveFormsModule],
  selector: 'app-chat-page',
  styleUrl: './chat-page.css',
  templateUrl: './chat-page.html',
})
export class ChatPage {
  private readonly chatService = inject(ChatService);
  private readonly fb = inject(NonNullableFormBuilder);
  private nextId = 1;
  private current: Subscription | null = null;

  protected readonly turns = signal<ChatTurn[]>([]);
  protected readonly products = signal<Product[]>([]);
  // Texto para lectores de pantalla: se anuncia cuando termina una respuesta, no cada palabra.
  protected readonly announcement = signal('');
  protected readonly busy = computed(() =>
    this.turns().some((t) => t.status === 'searching' || t.status === 'answering'),
  );
  protected readonly exampleQuestions = exampleQuestions;
  protected readonly splitCitations = splitCitations;

  protected readonly form = this.fb.group({
    question: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(1000)]],
    productId: this.fb.control<number | null>(null),
  });

  constructor() {
    inject(ProductService)
      .getAll()
      .pipe(takeUntilDestroyed())
      .subscribe({
        next: (products) => this.products.set(products),
        error: () => this.products.set([]),
      });
    // Si se sale de la página a mitad de una respuesta, se aborta la petición.
    inject(DestroyRef).onDestroy(() => this.current?.unsubscribe());
  }

  protected useExample(question: string): void {
    this.form.controls.question.setValue(question);
    this.ask();
  }

  protected ask(): void {
    if (this.form.invalid || this.busy()) {
      return;
    }

    const { question, productId } = this.form.getRawValue();
    const id = this.nextId++;
    this.turns.update((turns) => [
      ...turns,
      {
        id,
        question,
        answer: '',
        citations: [],
        status: 'searching',
        error: null,
        elapsedMilliseconds: null,
      },
    ]);
    this.form.controls.question.reset();
    this.announcement.set('');

    this.current = this.chatService.stream({ question, productId }).subscribe({
      next: (event) => this.apply(id, event),
      error: () =>
        this.update(id, {
          status: 'error',
          error: 'No se pudo conectar con el asistente. ¿Están arrancados la API y Ollama?',
        }),
    });
  }

  protected cancel(): void {
    this.current?.unsubscribe();
    const running = this.turns().find((t) => t.status === 'searching' || t.status === 'answering');
    if (running) {
      this.update(running.id, { status: 'cancelled' });
    }
  }

  protected isCited(turn: ChatTurn, citation: Citation): boolean {
    return citedNumbers(turn.answer).has(citation.number);
  }

  protected percentage(similarity: number): string {
    return `${Math.round(similarity * 100)} %`;
  }

  protected seconds(milliseconds: number): string {
    return `${(milliseconds / 1000).toFixed(1).replace('.', ',')} s`;
  }

  private apply(id: number, event: ChatStreamEvent): void {
    switch (event.type) {
      case 'sources':
        this.update(id, { citations: event.citations, status: 'answering' });
        break;
      case 'delta':
        this.turns.update((turns) =>
          turns.map((t) => (t.id === id ? { ...t, answer: t.answer + event.text } : t)),
        );
        break;
      case 'done':
        this.update(id, { status: 'done', elapsedMilliseconds: event.elapsedMilliseconds });
        this.announcement.set('Respuesta lista.');
        break;
      case 'error':
        this.update(id, { status: 'error', error: event.message });
        break;
    }
  }

  private update(id: number, changes: Partial<ChatTurn>): void {
    this.turns.update((turns) => turns.map((t) => (t.id === id ? { ...t, ...changes } : t)));
  }
}
