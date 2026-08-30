import { Injectable, computed, signal } from '@angular/core';
import { Observable, Subject } from 'rxjs';

export interface ConfirmationRequest {
  readonly id: number;
  readonly title: string;
  readonly message: string;
  readonly confirmText?: string;
  readonly cancelText?: string;
  readonly danger?: boolean;
}

interface PendingConfirmation extends ConfirmationRequest {
  readonly result: Subject<boolean>;
}

@Injectable({ providedIn: 'root' })
export class ConfirmationService {
  private nextId = 1;
  private readonly pending = signal<PendingConfirmation | null>(null);

  readonly current = computed<ConfirmationRequest | null>(() => this.pending());

  confirm(request: Omit<ConfirmationRequest, 'id'>): Observable<boolean> {
    const result = new Subject<boolean>();
    this.pending.set({ id: this.nextId++, ...request, result });
    return result.asObservable();
  }

  resolve(accepted: boolean): void {
    const current = this.pending();
    if (!current) {
      return;
    }

    current.result.next(accepted);
    current.result.complete();
    this.pending.set(null);
  }
}
