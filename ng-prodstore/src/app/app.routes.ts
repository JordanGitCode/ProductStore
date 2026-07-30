import { Routes } from '@angular/router';
import { ProdUpload } from './prod-upload/prod-upload';
import { Home } from './home/home';
import { ProductView } from './product-view/product-view';

export const routes: Routes = [
  { path: 'home', component: Home },
  { path: 'upload-product', component: ProdUpload },
  { path: 'product/:id', component: ProductView },
  { path: '', redirectTo: '/home', pathMatch: 'full' },
];
