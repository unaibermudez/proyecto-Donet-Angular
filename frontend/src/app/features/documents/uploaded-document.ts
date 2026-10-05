// Lo que devuelve /api/documents. Refleja DocumentResponse del backend.
// No se llama Document para no confundirlo con el Document del DOM (window.document).
export interface UploadedDocument {
  id: number;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedAt: string;
  productId: number | null;
  productName: string | null;
}

// Mismas reglas que DocumentFileRules y DocumentStorage:MaxFileSizeMegabytes en el
// backend. Aquí solo sirven para avisar antes de subir; la comprobación que manda es
// la del servidor.
export const allowedExtensions = ['.md', '.pdf'];
export const maxFileSizeMegabytes = 10;

// Motivo por el que no se puede subir el fichero, o null si se puede.
export function validateFile(file: File): string | null {
  const dot = file.name.lastIndexOf('.');
  const extension = dot === -1 ? '' : file.name.slice(dot).toLowerCase();
  if (!allowedExtensions.includes(extension)) {
    return 'Solo se pueden subir ficheros .md y .pdf.';
  }
  if (file.size === 0) {
    return 'El fichero está vacío.';
  }
  if (file.size > maxFileSizeMegabytes * 1024 * 1024) {
    return `El fichero no puede ocupar más de ${maxFileSizeMegabytes} MB.`;
  }
  return null;
}
