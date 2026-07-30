import { Component, OnInit, inject, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { FullProduct, ProductService } from '../services/product-service';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-home',
  imports: [CurrencyPipe, RouterLink],
  templateUrl: './home.html',
  styleUrl: './home.css',
})
export class Home implements OnInit {
  productService = inject(ProductService);
  products = signal<FullProduct[]>([]);
  loading = signal<boolean>(true);
  error = signal('');

  ngOnInit(): void {
    this.load();
  }

  // Exposed so the error state can offer a retry — on a phone a failed load is
  // usually a dropped connection, not something reloading the page should be needed for.
  load(): void {
    this.loading.set(true);
    this.error.set('');

    this.productService.getProductsWithImage().subscribe({
      next: (p) => {
        this.products.set(p);
        this.loading.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.error.set(
          err.status === 0
            ? 'Could not reach the server.'
            : `Could not load products (${err.status}).`,
        );
        this.loading.set(false);
      },
    });
  }
}
