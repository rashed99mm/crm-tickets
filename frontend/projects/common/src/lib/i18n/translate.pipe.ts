import { inject, Pipe, PipeTransform } from '@angular/core';
import { LocaleStore } from './locale.store';
import { TranslationKey } from './translations';

/**
 * `{{ 'tickets.queue.title' | t }}` — client-owned UI text, from the dictionary.
 *
 * The sibling of `LocalizePipe`, and deliberately a different pipe: `localize` takes a bilingual
 * message the SERVER sent, `t` takes a key the client owns. One pipe doing both would blur the line
 * the whole design rests on, and the argument types keep them apart at compile time.
 *
 * Named `t` because it appears in every template and a longer name would bury the text it wraps.
 *
 * **`pure: false` is load-bearing, and it is not what it looks like.** A pure pipe is memoised on
 * its ARGUMENTS: reading the locale signal inside `transform` does mark the view dirty, so the view
 * refreshes — and then `ɵɵpipeBind` sees the same key it saw last time and hands back the cached
 * English string without calling `transform` at all. The label never changes. The plan assumed
 * otherwise; a test caught it (`AC63: the translate pipe re-renders text on switch`).
 *
 * The cost is a dictionary lookup per change detection pass on a view that was already dirty, which
 * is nothing. Re-rendering from the dictionary is still what makes AC-68 hold: no request is issued.
 */
@Pipe({ name: 't', pure: false })
export class TranslatePipe implements PipeTransform {
  private readonly locale = inject(LocaleStore);

  transform(key: TranslationKey, ...params: readonly (string | number)[]): string {
    return this.locale.t(key, ...params);
  }
}
