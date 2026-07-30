import { Service } from '@angular/core';
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export type ScanStatus = 'Pending' | 'Completed' | 'Failed';

export interface Product {
  id: string;
  productCategoryId: string;
  name: string;
  description: string | null;
  price: number;
  scanStatus: ScanStatus;
  suggestedName: string | null;
  suggestedDescription: string | null;
  suggestedCategory: string | null;
}

export interface FullProduct {
  id: string;
  productCategoryId: string;
  categoryName: string;
  name: string;
  description: string | null;
  price: number;
  imageId: string | null;
}

export interface UpdateProduct {
  productCategoryId: string;
  name: string;
  description: string | null;
  price: number;
}

export interface ProductCategory {
  id: string;
  name: string;
  description: string | null;
}

export interface ProductImage {
  id: string;
  productId: string;
  content: string;
  contentType: string;
}

@Injectable({ providedIn: 'root' })
export class ProductService {
  private http = inject(HttpClient);
  // Same-origin paths, routed to the API by proxy.conf.json. Keeping these relative
  // is what lets the app work unchanged over a cloudflared tunnel from a phone —
  // an absolute localhost URL would resolve to the phone itself.
  private baseUrl = '/product';
  private categoryUrl = '/category';

  // getProducts(): Observable<Product[]> {
  //   return this.http.get<Product[]>(this.baseUrl);
  // }

  getProductsWithImage(): Observable<FullProduct[]> {
    return this.http.get<FullProduct[]>(this.baseUrl);
  }

  // max is the longest edge in pixels; omit it for the original bytes. Requesting a
  // size matters on mobile — the stored originals are multi-megabyte phone photos.
  imageUrl(productId: string, imageId: string, max?: number): string {
    const url = `${this.baseUrl}/${productId}/image/${imageId}`;
    return max ? `${url}?max=${max}` : url;
  }

  getProductById(id: string): Observable<Product> {
    return this.http.get<Product>(`${this.baseUrl}/${id}`);
  }

  updateProduct(id: string, update: UpdateProduct): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, update);
  }

  getCategories(): Observable<ProductCategory[]> {
    return this.http.get<ProductCategory[]>(this.categoryUrl);
  }

  createCategory(name: string): Observable<ProductCategory> {
    return this.http.post<ProductCategory>(this.categoryUrl, { name });
  }

  // FormData, so the browser sets the multipart Content-Type and boundary itself.
  createProduct(form: FormData): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.baseUrl, form);
  }

  getImageIds(id: string): Observable<string[]> {
    return this.http.get<string[]>(`${this.baseUrl}/${id}/image`);
  }

  getProductImages(id: string): Observable<ProductImage[]> {
    return this.http.get<ProductImage[]>(`${this.baseUrl}/images/${id}`);
  }
}
