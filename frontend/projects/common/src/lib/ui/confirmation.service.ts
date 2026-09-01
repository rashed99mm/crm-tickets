import { Injectable, computed, signal } from '@angular/core';
import { Observable, Subject } from 'rxjs';

export interface ConfirmationRequest {
  readonly id: number;
  readonly title: string;
  readonly message: string;
  /**
   * Optional lines rendered as a list under the message — used by the permission workbench to show
   * exactly which grants and revokes a save will apply (AC-806.12, AC-807.4). Kept as plain strings
   * rather than a structured type: the dialog's job is to display them, not to interpret them.
   */
  readonly details?: readonly string[];
  readonly confirmText?: string;
  readonly cancelText?: string;
  readonly danger?: boolean;
}

interface PendingConfirmation extends ConfirmationRequest {
  readonly result: Subject<boolean>;
}

/**
 * One dialog at a time, FIFO — never one dialog *instead of* another.
 *
 * The previous implementation held a single `pending` signal and overwrote it, so a second
 * `confirm()` silently discarded the first request's `Subject` without emitting or completing it,
 * leaving that caller waiting forever (AC-807.1). Two prompts can now genuinely overlap: the
 * permissions screen's save dialog and its leave-the-page dialog.
 *
 * FIFO, not newest-wins: replacing a dialog under the user's cursor answers a question they were
 * not reading.
 */
@Injectable({ providedIn: 'root' })
export class ConfirmationService {
  private nextId = 1;
  private readonly queue = signal<readonly PendingConfirmation[]>([]);

  readonly current = computed<ConfirmationRequest | null>(() => this.queue()[0] ?? null);
  readonly pendingCount = computed(() => this.queue().length);

  confirm(request: Omit<ConfirmationRequest, 'id'>): Observable<boolean> {
    const result = new Subject<boolean>();
    this.queue.update((queue) => [...queue, { id: this.nextId++, ...request, result }]);
    return result.asObservable();
  }

  resolve(accepted: boolean): void {
    const [head, ...rest] = this.queue();
    if (!head) {
      return;
    }

    // Dequeue BEFORE emitting. A subscriber commonly opens a follow-up dialog from inside its own
    // callback; if the emit came first, that new request would be dropped by this same update.
    this.queue.set(rest);
    head.result.next(accepted);
    head.result.complete();
  }
}
