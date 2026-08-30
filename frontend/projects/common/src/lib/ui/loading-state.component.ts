import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { LocaleStore } from '../i18n/locale.store';

/**
 * The loading state. Announced via role="status" so a screen reader knows
 * something is in flight rather than the page simply being empty.
 */
@Component({
  selector: 'cs-loading-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './loading-state.component.html',
})
export class CsLoadingState {
  private readonly locale = inject(LocaleStore);

  /**
   * What is loading, already localised by the caller — usually `'notes.loading' | t`.
   *
   * Defaults to null rather than to "Loading": a literal default would be an English string
   * baked into the library, invisible to AC-63's sweep because it lives in TypeScript.
   */
  readonly label = input<string | null>(null);

  readonly text = computed(() => this.label() ?? this.locale.t('state.loading'));
}
