import { AbstractControl, ValidationErrors } from '@angular/forms';

// Validadores propios que repiten reglas del backend (ProductRequest).
// Un campo vacío cuenta como válido: de eso se encarga Validators.required.

// Stock, RAM y almacenamiento son int en el backend: 1.5 no se puede convertir.
export function integer(control: AbstractControl): ValidationErrors | null {
  const value = control.value;
  if (value === null || value === '') {
    return null;
  }
  return Number.isInteger(value) ? null : { integer: true };
}

// La fecha de lanzamiento no puede estar a más de un año vista.
// El valor llega del <input type="date"> como texto "AAAA-MM-DD", que se puede comparar
// como texto. Se usa UTC, igual que el backend.
export function notMoreThanOneYearAhead(control: AbstractControl): ValidationErrors | null {
  const value = control.value as string;
  if (!value) {
    return null;
  }
  const limit = new Date();
  limit.setUTCFullYear(limit.getUTCFullYear() + 1);
  return value > limit.toISOString().slice(0, 10) ? { tooFarAhead: true } : null;
}
