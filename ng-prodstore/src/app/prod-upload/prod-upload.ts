import { Component, ElementRef, OnDestroy, effect, inject, signal, viewChild } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { ProductService } from '../services/product-service';

@Component({
  selector: 'app-prod-upload',
  imports: [],
  templateUrl: './prod-upload.html',
  styleUrl: './prod-upload.css',
})
export class ProdUpload implements OnDestroy {
  private productService = inject(ProductService);
  private router = inject(Router);

  photos = signal<File[]>([]);
  previews = signal<string[]>([]);
  submitting = signal(false);
  error = signal('');

  // The <video> only exists while the viewfinder is open, so this is empty until then.
  private videoEl = viewChild<ElementRef<HTMLVideoElement>>('video');

  cameraOpen = signal(false);
  cameraError = signal('');
  stream = signal<MediaStream | null>(null);

  constructor() {
    // The element is created by @if a render after cameraOpen flips, so the stream
    // can't be attached inline in openCamera() — this waits for both to exist.
    effect(() => {
      const video = this.videoEl()?.nativeElement;
      const stream = this.stream();
      if (!video || !stream || video.srcObject === stream) return;

      video.srcObject = stream;
      // Set here as well as in markup: iOS only honours autoplay when muted.
      video.muted = true;
      video.play().catch(() => this.cameraError.set('Could not start the camera preview.'));
    });
  }

  async openCamera(): Promise<void> {
    this.cameraError.set('');

    if (!navigator.mediaDevices?.getUserMedia) {
      this.cameraError.set('This browser does not support in-app camera capture.');
      return;
    }

    // Opened before the permission prompt resolves so the user gets the spinner
    // rather than a dead button while they decide.
    this.cameraOpen.set(true);

    try {
      const stream = await navigator.mediaDevices.getUserMedia({
        video: {
          facingMode: { ideal: 'environment' },
          width: { ideal: 1920 },
          height: { ideal: 1080 },
        },
        audio: false,
      });

      // Closed again while permission was pending — drop the stream we just opened,
      // otherwise the camera stays live with nothing showing it.
      if (!this.cameraOpen()) {
        for (const track of stream.getTracks()) track.stop();
        return;
      }

      this.stream.set(stream);
    } catch (err) {
      this.cameraOpen.set(false);
      this.cameraError.set(this.toCameraMessage(err));
    }
  }

  // Stays open after a shot: taking several photos in a row is the point.
  capturePhoto(): void {
    const video = this.videoEl()?.nativeElement;
    if (!video || !video.videoWidth) return;

    const canvas = document.createElement('canvas');
    canvas.width = video.videoWidth;
    canvas.height = video.videoHeight;

    const context = canvas.getContext('2d');
    if (!context) return;
    context.drawImage(video, 0, 0);

    canvas.toBlob(
      (blob) => {
        if (!blob) return;
        this.addFiles([new File([blob], `photo-${Date.now()}.jpg`, { type: 'image/jpeg' })]);
      },
      'image/jpeg',
      0.9,
    );
  }

  closeCamera(): void {
    for (const track of this.stream()?.getTracks() ?? []) {
      track.stop();
    }

    this.stream.set(null);
    this.cameraOpen.set(false);
  }

  onPhotosSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.addFiles(Array.from(input.files ?? []));

    // Clearing the input lets the next selection fire change again — otherwise picking
    // the same file, or re-opening the camera, is a no-op because value is unchanged.
    input.value = '';
  }

  private addFiles(files: File[]): void {
    if (files.length === 0) return;

    this.photos.update((photos) => [...photos, ...files]);
    this.previews.update((previews) => [...previews, ...files.map((f) => URL.createObjectURL(f))]);
    this.error.set('');
  }

  removePhoto(index: number): void {
    URL.revokeObjectURL(this.previews()[index]);

    this.photos.update((photos) => photos.filter((_, i) => i !== index));
    this.previews.update((previews) => previews.filter((_, i) => i !== index));
  }

  submit(event: Event): void {
    // Native submit event: stop the browser from reloading the page.
    event.preventDefault();

    if (this.photos().length === 0) {
      this.error.set('At least one photo is required.');
      return;
    }

    this.submitting.set(true);
    this.error.set('');

    const form = new FormData();
    for (const photo of this.photos()) {
      form.append('photos', photo);
    }

    this.productService.createProduct(form).subscribe({
      next: (product) => this.router.navigate(['/product', product.id]),
      error: (err: HttpErrorResponse) => {
        this.error.set(this.toMessage(err));
        this.submitting.set(false);
      },
    });
  }

  // Without this the camera keeps recording after navigating away — the device's
  // camera indicator stays lit until the tab is closed.
  ngOnDestroy(): void {
    this.closeCamera();

    for (const url of this.previews()) {
      URL.revokeObjectURL(url);
    }
  }

  private toCameraMessage(err: unknown): string {
    const name = (err as DOMException)?.name;

    if (name === 'NotAllowedError') {
      return 'Camera access was blocked. Allow it in your browser settings, or choose photos instead.';
    }
    if (name === 'NotFoundError') {
      return 'No camera was found on this device.';
    }
    if (name === 'NotReadableError') {
      return 'The camera is already in use by another app.';
    }

    return 'Could not open the camera.';
  }

  private toMessage(err: HttpErrorResponse): string {
    if (err.status === 0) {
      return 'Could not reach the server.';
    }
    if (typeof err.error === 'string') {
      return err.error;
    }

    const errors = err.error?.errors as Record<string, string[]> | undefined;
    if (errors) {
      return Object.values(errors).flat().join(' ');
    }

    return err.error?.title ?? `Upload failed (${err.status}).`;
  }
}
