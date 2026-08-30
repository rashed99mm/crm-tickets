import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { interval } from 'rxjs';
import { LocaleStore } from '../i18n/locale.store';

export type SlaCountdownUrgency = 'normal' | 'warning' | 'danger';

/**
 * A live countdown to an SLA due date — US-222, AC-155/AC-156.
 *
 * Warning/danger are derived from the window between `createdAt` and `dueAt` (spec addendum A6):
 * no field anywhere carries an explicit warning threshold, so the whole window is treated as 100%
 * and the countdown crosses into warning once less than 20% of it remains, and danger once the due
 * date has passed.
 */
@Component({
  selector: 'cs-sla-countdown',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './sla-countdown.component.html',
})
export class SlaCountdown {
  private readonly locale = inject(LocaleStore);

  readonly dueAt = input<string | null>(null);
  readonly createdAt = input.required<string>();
  readonly label = input.required<string>();

  private readonly now = signal(Date.now());

  constructor() {
    const destroyRef = inject(DestroyRef);
    interval(1000)
      .pipe(takeUntilDestroyed(destroyRef))
      .subscribe(() => this.now.set(Date.now()));
  }

  private readonly remainingMs = computed<number | null>(() => {
    const due = this.dueAt();
    return due ? new Date(due).getTime() - this.now() : null;
  });

  readonly urgency = computed<SlaCountdownUrgency>(() => {
    const remaining = this.remainingMs();
    const due = this.dueAt();
    if (remaining === null || !due) {
      return 'normal';
    }
    if (remaining <= 0) {
      return 'danger';
    }

    const totalMs = new Date(due).getTime() - new Date(this.createdAt()).getTime();
    if (totalMs <= 0) {
      return 'danger';
    }

    return remaining / totalMs < 0.2 ? 'warning' : 'normal';
  });

  readonly dotClass = computed(
    () =>
      ({
        normal: 'bg-on-surface-variant',
        warning: 'bg-warning',
        danger: 'bg-error',
      })[this.urgency()],
  );

  readonly textClass = computed(
    () =>
      ({
        normal: 'text-on-surface',
        warning: 'text-warning',
        danger: 'text-error',
      })[this.urgency()],
  );

  readonly remainingLabel = computed(() => {
    const remaining = this.remainingMs();
    if (remaining === null) {
      return '';
    }

    const formatted = this.formatDuration(Math.abs(remaining));
    return remaining <= 0
      ? this.locale.t('sla.countdown.overdue', formatted)
      : this.locale.t('sla.countdown.left', formatted);
  });

  private formatDuration(ms: number): string {
    const totalMinutes = Math.floor(ms / 60_000);
    const days = Math.floor(totalMinutes / 1440);
    const hours = Math.floor((totalMinutes % 1440) / 60);
    const minutes = totalMinutes % 60;
    if (days > 0) {
      return `${days}d ${hours}h`;
    }
    if (hours > 0) {
      return `${hours}h ${minutes}m`;
    }
    return `${minutes}m`;
  }
}
