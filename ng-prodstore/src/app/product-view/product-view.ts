import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Subscription, switchMap, takeWhile, timer } from 'rxjs';
import { Product, ProductCategory, ProductService } from '../services/product-service';

@Component({
  selector: 'app-product-view',
  imports: [CurrencyPipe, RouterLink, FormsModule],
  templateUrl: './product-view.html',
  styleUrl: './product-view.css',
})
export class ProductView implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  protected productService = inject(ProductService);

  private poll?: Subscription;

  product = signal<Product | null>(null);
  imageIds = signal<string[]>([]);
  loading = signal(true);
  loadingContent = signal(true);
  error = signal('');

  imageIndex = signal(0);
  imageCount = computed(() => this.imageIds().length);
  currentImageId = computed(() => this.imageIds()[this.imageIndex()] ?? null);

  categories = signal<ProductCategory[]>([]);
  editing = signal(false);
  saving = signal(false);
  saveError = signal('');

  editCategoryId = '';
  editName = '';
  editDescription = '';
  editPrice = 0;

  // Sentinel for "create the suggested category on save".
  readonly CREATE_CATEGORY = '__create__';

  // True when the suggested category doesn't match any existing one.
  suggestedCategoryIsNew = computed(() => {
    const suggested = this.product()?.suggestedCategory?.trim();
    if (!suggested) return false;
    return !this.findCategory(suggested);
  });

  private findCategory(name: string): ProductCategory | undefined {
    return this.categories().find((c) => c.name.toLowerCase() === name.trim().toLowerCase());
  }

  // A listing nobody has edited yet: still carrying the placeholders from upload.
  private isUntouched(product: Product): boolean {
    return product.name === 'Untitled' && product.price === 0 && !product.description;
  }

  startEdit(): void {
    const product = this.product();
    if (!product) return;

    this.editCategoryId = product.productCategoryId;
    this.editName = product.name;
    this.editDescription = product.description ?? '';
    this.editPrice = product.price;
    this.saveError.set('');
    this.editing.set(true);

    if (this.categories().length > 0) {
      this.prefillIfUntouched(product);
      return;
    }

    this.productService.getCategories().subscribe({
      next: (c) => {
        this.categories.set(c);
        this.prefillIfUntouched(product);
      },
      error: () => this.saveError.set('Could not load categories.'),
    });
  }

  // Categories have to be loaded first, since the category suggestion resolves against them.
  private prefillIfUntouched(product: Product): void {
    if (product.scanStatus !== 'Completed' || !this.isUntouched(product)) return;

    this.useName();
    this.useDescription();
    this.useCategory();
  }

  useName(): void {
    const suggested = this.product()?.suggestedName?.trim();
    if (suggested) this.editName = suggested;
  }

  useDescription(): void {
    const suggested = this.product()?.suggestedDescription?.trim();
    if (suggested) this.editDescription = suggested;
  }

  useCategory(): void {
    const suggested = this.product()?.suggestedCategory?.trim();
    if (!suggested) return;

    const match = this.findCategory(suggested);
    this.editCategoryId = match ? match.id : this.CREATE_CATEGORY;
  }

  cancelEdit(): void {
    this.editing.set(false);
  }

  save(event: Event): void {
    event.preventDefault();

    const product = this.product();
    if (!product) return;

    this.saving.set(true);
    this.saveError.set('');

    // The suggested category doesn't exist yet — create it, then save with its id.
    if (this.editCategoryId === this.CREATE_CATEGORY) {
      const name = product.suggestedCategory?.trim();
      if (!name) {
        this.saveError.set('Please choose a category.');
        this.saving.set(false);
        return;
      }

      this.productService.createCategory(name).subscribe({
        next: (category) => {
          this.categories.update((list) => [...list, category]);
          this.editCategoryId = category.id;
          this.persist(product);
        },
        error: (err: HttpErrorResponse) => this.failSave(err),
      });
      return;
    }

    this.persist(product);
  }

  private persist(product: Product): void {
    const update = {
      productCategoryId: this.editCategoryId,
      name: this.editName.trim(),
      description: this.editDescription.trim() || null,
      price: this.editPrice,
    };

    this.productService.updateProduct(product.id, update).subscribe({
      next: () => {
        // PUT returns 204, so patch the local copy rather than refetching.
        this.product.set({ ...product, ...update });
        this.editing.set(false);
        this.saving.set(false);
      },
      error: (err: HttpErrorResponse) => this.failSave(err),
    });
  }

  private failSave(err: HttpErrorResponse): void {
    this.saveError.set(
      err.status === 0
        ? 'Could not reach the server.'
        : typeof err.error === 'string'
          ? err.error
          : `Save failed (${err.status}).`,
    );
    this.saving.set(false);
  }

  nextImage(): void {
    if (this.imageCount() === 0) return;
    this.imageIndex.update((i) => (i + 1) % this.imageCount());
  }

  previousImage(): void {
    if (this.imageCount() === 0) return;
    this.imageIndex.update((i) => (i - 1 + this.imageCount()) % this.imageCount());
  }

  loadImages(id: string): void {
    this.productService.getImageIds(id).subscribe({
      next: (ids) => {
        this.imageIds.set(ids);
        this.loadingContent.set(false);
      },
      error: () => this.loadingContent.set(false),
    });
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');

    if (!id) {
      this.error.set('No product id in the URL.');
      this.loading.set(false);
      return;
    }

    this.productService.getProductById(id).subscribe({
      next: (p) => {
        this.product.set(p);
        this.loading.set(false);
        this.loadImages(id);

        if (p.scanStatus === 'Pending') {
          this.pollScanStatus(id);
        }
      },
      error: (err: HttpErrorResponse) => {
        this.error.set(
          err.status === 0
            ? 'Could not reach the server.'
            : err.status === 404
              ? 'That product does not exist.'
              : `Request failed (${err.status}).`,
        );
        this.loading.set(false);
      },
    });
  }

  // A scan runs in the background, so poll until it settles. takeWhile(..., true)
  // emits the final non-Pending value, then completes on its own.
  private pollScanStatus(id: string): void {
    this.poll = timer(3000, 3000)
      .pipe(
        switchMap(() => this.productService.getProductById(id)),
        takeWhile((p) => p.scanStatus === 'Pending', true),
      )
      .subscribe({
        next: (p) => this.product.set(p),
        error: () => this.poll?.unsubscribe(),
      });
  }

  ngOnDestroy(): void {
    this.poll?.unsubscribe();
  }
}
