import { inject, Pipe, PipeTransform } from '@angular/core';
import { LocaleStore } from './locale.store';

/**
 * `{{ ticket.createdAt | csDate }}` — an ISO instant as a person reads it.
 *
 * Written because every list and the ticket history were rendering the raw wire value
 * (`2026-08-25T22:01:56.4371Z`), which is unreadable, overflows its column, and looks like a
 * defect to anyone who opens the app. Visual verification against the mockups is what surfaced
 * it; no test asserted the rendered text, so it had gone unnoticed since the first list shipped.
 *
 * `Intl` rather than Angular's `DatePipe`: `DatePipe` needs `registerLocaleData` per language and
 * a `LOCALE_ID` that is fixed at injection time, which a runtime toggle cannot change. `Intl` is
 * in the platform, takes the locale as an argument, and gives Arabic its own numerals for free.
 *
 * `pure: false`, for the reason its two siblings are: a pure pipe memoises on its argument, so
 * the same instant would keep its English rendering forever after a switch to Arabic.
 */
@Pipe({ name: 'csDate', pure: false })
export class CsDatePipe implements PipeTransform {
  private readonly locale = inject(LocaleStore);

  transform(value: string | Date | null | undefined, mode: 'date' | 'datetime' = 'datetime'): string {
    if (!value) {
      return '';
    }

    const instant = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(instant.getTime())) {
      // A malformed instant is a server contract problem. Showing the raw value makes it
      // reportable; swallowing it to '' would hide it in exactly the way the empty-vs-error
      // rule forbids elsewhere.
      return String(value);
    }

    const tag = this.locale.locale() === 'ar' ? 'ar-EG' : 'en-GB';
    const options: Intl.DateTimeFormatOptions =
      mode === 'date'
        ? { year: 'numeric', month: 'short', day: 'numeric' }
        : { year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' };

    return new Intl.DateTimeFormat(tag, options).format(instant);
  }
}
