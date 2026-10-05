import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'products' },
  {
    path: 'products',
    loadComponent: () =>
      import('./features/products/product-list/product-list').then((m) => m.ProductList),
    title: 'Productos · Tienda Tech',
  },
  {
    path: 'products/new',
    loadComponent: () =>
      import('./features/products/product-form/product-form').then((m) => m.ProductForm),
    title: 'Nuevo producto · Tienda Tech',
  },
  {
    path: 'products/:id/edit',
    loadComponent: () =>
      import('./features/products/product-form/product-form').then((m) => m.ProductForm),
    title: 'Editar producto · Tienda Tech',
  },
  {
    path: 'documents',
    loadComponent: () =>
      import('./features/documents/document-list/document-list').then((m) => m.DocumentList),
    title: 'Documentos · Tienda Tech',
  },
];
