import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';
import { ApiError } from '../api/api-error';
import { LocaleStore } from '../i18n/locale.store';
import { CsButton } from './button.component';
import { CsIcon } from './icon.component';

/**
 * The error state — a request that failed.
 *
 * Carries a retry button and the trace id, and is visually distinct from the
 * empty state (AC-58). If these two looked alike, a server failure would read
 * as "no data" and the real fault would go unreported. The restyle keeps that
 * distance deliberately wide: a red-tinted panel, a filled `error` glyph and
 * `role="alert"`, against the empty state's neutral `inbox`.
 */
@Component({
  selector: 'cs-error-state',
  imports: [CsButton, CsIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './error-state.component.html',
})
export class CsErrorState {
  /** Protected, not private: the retry label is read from the template. */
  protected readonly locale = inject(LocaleStore);

  readonly error = input.required<ApiError>();
  readonly retry = output<void>();

  title(): string {
    return this.isNetworkError()
      ? this.locale.t('error.network.title')
      : this.locale.t('error.generic.title');
  }

  text(): string {
    return this.isNetworkError()
      ? this.locale.t('error.network.message')
      : this.error().message_;
  }

  retryLabel(): string {
    return this.locale.t('action.retry');
  }

  icon(): string {
    return 'warning';
  }

  private isNetworkError(): boolean {
    const failure = this.error();
    return failure.status === 0 || failure.code === 'NETWORK_ERROR';
  }
}
