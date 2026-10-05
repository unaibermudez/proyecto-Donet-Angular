// Un evento de Server-Sent Events: "event: delta\ndata: {...}\n\n".
export interface SseMessage {
  event: string;
  data: string;
}

// Lee Server-Sent Events de un texto que llega a trozos. Un trozo puede traer medio
// evento o varios: el parser guarda lo incompleto hasta que llega la línea en blanco que
// cierra cada evento. (EventSource, la API del navegador para SSE, solo admite GET; para
// un POST hay que leer la respuesta de fetch y separar los eventos a mano.)
export class SseParser {
  private buffer = '';

  push(chunk: string): SseMessage[] {
    this.buffer += chunk.replace(/\r\n/g, '\n');
    const messages: SseMessage[] = [];

    let end: number;
    while ((end = this.buffer.indexOf('\n\n')) !== -1) {
      const block = this.buffer.slice(0, end);
      this.buffer = this.buffer.slice(end + 2);

      let event = 'message';
      const data: string[] = [];
      for (const line of block.split('\n')) {
        if (line.startsWith('event:')) {
          event = line.slice('event:'.length).trim();
        } else if (line.startsWith('data:')) {
          data.push(line.slice('data:'.length).replace(/^ /, ''));
        }
      }
      if (data.length > 0) {
        messages.push({ event, data: data.join('\n') });
      }
    }

    return messages;
  }
}
