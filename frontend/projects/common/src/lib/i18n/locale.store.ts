import { computed, effect, Injectable, signal } from '@angular/core';
import { LocalizedMessage } from '../api/api-response';
import { TRANSLATIONS, TranslationKey } from './translations';

export type Locale = 'ar' | 'en';

const KEY = 'cs.locale';

function initial(): Locale {
  try {
    return localStorage.getItem(KEY) === 'ar' ? 'ar' : 'en';
  } catch {
    return 'en';
  }
}

/**
 * The single source of language for the whole application.
 *
 * Changing it re-renders from data already in hand: the backend sends both
 * languages in every response (ADR 0007), so switching must never refetch.
 * A test asserts exactly that, because a refactor to "reload with the new
 * locale" would look reasonable, still work, and quietly throw away the
 * reason the envelope carries two languages at all.
 */
@Injectable({ providedIn: 'root' })
export class LocaleStore {
  private readonly _locale = signal<Locale>(initial());

  readonly locale = this._locale.asReadonly();

  readonly direction = computed<'rtl' | 'ltr'>(() =>
    this._locale() === 'ar' ? 'rtl' : 'ltr',
  );

  constructor() {
    effect(() => {
      const locale = this._locale();

      document.documentElement.lang = locale;
      document.documentElement.dir = locale === 'ar' ? 'rtl' : 'ltr';

      try {
        localStorage.setItem(KEY, locale);
      } catch {
        // Non-fatal: the choice just will not survive a reload.
      }
    });
  }

  setLocale(locale: Locale): void {
    this._locale.set(locale);
  }

  toggle(): void {
    this._locale.update((current) => (current === 'ar' ? 'en' : 'ar'));
  }

  /** Picks the active half of a bilingual message. */
  resolve(message: LocalizedMessage): string {
    return this._locale() === 'ar' ? message.ar : message.en;
  }

  /**
   * UI text by key. Named `t()` rather than `translate()` because it appears in every template.
   *
   * Reads the locale signal, so a caller inside a template or a computed re-evaluates on switch
   * with no refetch — that is what makes AC-68's no-request property hold for client text as well
   * as for server messages.
   *
   * `params` fill `{0}`, `{1}` … positionally. Keeping the whole sentence in the dictionary lets a
   * translator move the value to wherever the target grammar needs it.
   */
  t(key: TranslationKey, ...params: readonly (string | number)[]): string {
    const text = this.resolve(TRANSLATIONS[key]);

    return params.length === 0
      ? text
      : text.replace(/\{(\d+)\}/g, (whole, index: string) => {
          const value = params[Number(index)];
          // An unsupplied placeholder is left visible rather than blanked: a stray "{1}" on screen
          // is a bug report, whereas a silently missing filename reads as working software.
          return value === undefined ? whole : String(value);
        });
  }
}
