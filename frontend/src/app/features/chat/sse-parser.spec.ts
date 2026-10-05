import { SseParser } from './sse-parser';

describe('SseParser', () => {
  it('separa varios eventos que llegan en el mismo trozo', () => {
    const parser = new SseParser();

    const messages = parser.push(
      'event: sources\ndata: {"citations":[]}\n\nevent: delta\ndata: {"text":"Hola"}\n\n',
    );

    expect(messages).toEqual([
      { event: 'sources', data: '{"citations":[]}' },
      { event: 'delta', data: '{"text":"Hola"}' },
    ]);
  });

  it('guarda un evento a medias hasta que llega el resto', () => {
    const parser = new SseParser();

    expect(parser.push('event: delta\ndata: {"te')).toEqual([]);
    expect(parser.push('xt":"Hola"}\n')).toEqual([]);
    expect(parser.push('\n')).toEqual([{ event: 'delta', data: '{"text":"Hola"}' }]);
  });

  it('entiende saltos de línea de Windows y varias líneas data', () => {
    const parser = new SseParser();

    const messages = parser.push('data: línea 1\r\ndata: línea 2\r\n\r\n');

    expect(messages).toEqual([{ event: 'message', data: 'línea 1\nlínea 2' }]);
  });
});
