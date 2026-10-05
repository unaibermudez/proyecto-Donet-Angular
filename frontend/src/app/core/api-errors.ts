import { HttpErrorResponse } from '@angular/common/http';

// Mensajes de un error de validación de la API (400 con ValidationProblemDetails, que
// trae los errores agrupados por campo). Lista vacía si el error es de otro tipo.
export function validationMessages(error: HttpErrorResponse): string[] {
  if (error.status === 400 && error.error?.errors) {
    return Object.values(error.error.errors as Record<string, string[]>).flat();
  }
  return [];
}
