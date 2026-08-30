import { Injectable, computed, signal } from '@angular/core';

export type ToastKind = 'success' | 'error' | 'info' | 'warning';

export interface ToastMessage {
  readonly id: number;
  readonly kind: ToastKind;
  readonly title: string;
  readonly message?: string;
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  private nextId = 1;
  private readonly itemsSignal = signal<readonly ToastMessage[]>([]);

  readonly items = computed(() => this.itemsSignal());

  success(title: string, message?: string): void {
    this.show('success', title, message);
  }

  error(title: string, message?: string): void {
    this.show('error', title, message);
  }

  info(title: string, message?: string): void {
    this.show('info', title, message);
  }

  warning(title: string, message?: string): void {
    this.show('warning', title, message);
  }

  show(kind: ToastKind, title: string, message?: string): void {
    const toast = { id: this.nextId++, kind, title, message };
    this.itemsSignal.update((items) => [toast, ...items].slice(0, 5));
    window.setTimeout(() => this.dismiss(toast.id), 5_000);
  }

  dismiss(id: number): void {
    this.itemsSignal.update((items) => items.filter((toast) => toast.id !== id));
  }
}
