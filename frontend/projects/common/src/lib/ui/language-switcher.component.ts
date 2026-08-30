import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { LocaleStore } from '../i18n/locale.store';

/**
 * Designed here — no mockup contains a language switcher.
 *
 * Shows the language it will switch TO, which is the convention users expect
 * from a two-language toggle. The visible label is two characters, so an
 * aria-label carries the actual meaning.
 */
@Component({
  selector: 'cs-language-switcher',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './language-switcher.component.html',
})
export class CsLanguageSwitcher {
  protected readonly locale = inject(LocaleStore);
}
