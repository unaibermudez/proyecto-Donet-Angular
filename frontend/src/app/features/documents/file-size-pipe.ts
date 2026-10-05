import { formatNumber } from '@angular/common';
import { inject, LOCALE_ID, Pipe, PipeTransform } from '@angular/core';

// Tamaño de fichero legible: 512 B, 3,4 KB, 1,2 MB. Usa el idioma de la app (coma decimal).
@Pipe({ name: 'fileSize' })
export class FileSizePipe implements PipeTransform {
  private readonly locale = inject(LOCALE_ID);

  transform(bytes: number): string {
    if (bytes < 1024) {
      return `${bytes} B`;
    }
    if (bytes < 1024 * 1024) {
      return `${formatNumber(bytes / 1024, this.locale, '1.0-1')} KB`;
    }
    return `${formatNumber(bytes / 1024 / 1024, this.locale, '1.0-1')} MB`;
  }
}
