import { registerLocaleData } from '@angular/common';
import localeEs from '@angular/common/locales/es';
import { LOCALE_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FileSizePipe } from './file-size-pipe';

describe('FileSizePipe', () => {
  let pipe: FileSizePipe;

  beforeEach(() => {
    registerLocaleData(localeEs);
    TestBed.configureTestingModule({ providers: [{ provide: LOCALE_ID, useValue: 'es' }] });
    // El pipe usa inject(), así que se crea dentro del contexto de inyección del TestBed.
    pipe = TestBed.runInInjectionContext(() => new FileSizePipe());
  });

  it('deja los bytes tal cual por debajo de 1 KB', () => {
    expect(pipe.transform(512)).toBe('512 B');
  });

  it('pasa a KB con un decimal y coma', () => {
    expect(pipe.transform(3482)).toBe('3,4 KB');
  });

  it('pasa a MB a partir de 1 MB', () => {
    expect(pipe.transform(5 * 1024 * 1024)).toBe('5 MB');
  });
});
