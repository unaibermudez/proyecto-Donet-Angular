import { citedNumbers, splitCitations } from './chat';

describe('splitCitations', () => {
  it('separa las citas del texto', () => {
    expect(splitCitations('Dura tres años [1]. Se pide en la web [2].')).toEqual([
      { kind: 'text', text: 'Dura tres años ' },
      { kind: 'citation', number: 1 },
      { kind: 'text', text: '. Se pide en la web ' },
      { kind: 'citation', number: 2 },
      { kind: 'text', text: '.' },
    ]);
  });

  it('entiende citas juntas y con comas', () => {
    expect(splitCitations('Sí [1][3], y también [2, 4]')).toEqual([
      { kind: 'text', text: 'Sí ' },
      { kind: 'citation', number: 1 },
      { kind: 'citation', number: 3 },
      { kind: 'text', text: ', y también ' },
      { kind: 'citation', number: 2 },
      { kind: 'citation', number: 4 },
    ]);
  });

  it('un texto sin citas es un solo segmento', () => {
    expect(splitCitations('No tengo esa información.')).toEqual([
      { kind: 'text', text: 'No tengo esa información.' },
    ]);
  });

  it('citedNumbers devuelve los números citados', () => {
    expect([...citedNumbers('A [2]. B [2][4].')]).toEqual([2, 4]);
  });
});
