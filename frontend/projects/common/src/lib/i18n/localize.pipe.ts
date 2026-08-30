import { inject, Pipe, PipeTransform } from '@angular/core';
import { LocalizedMessage } from '../api/api-response';
import { LocaleStore } from './locale.store';

/**
 * `{{ someMessage | localize }}`
 *
 * `pure: false`, for the same reason `TranslatePipe` is. The previous comment here claimed the
 * opposite — that reading the signal was enough — and it was wrong: a pure pipe is memoised on its
 * argument, so the same message object returns the cached English half forever, however dirty the
 * view is. Nothing used this pipe yet, so the defect had never been observed; its sibling's test
 * exposed the shared mistake (MVP-13, AC-68).
 */
@Pipe({ name: 'localize', pure: false })
export class LocalizePipe implements PipeTransform {
  private readonly locale = inject(LocaleStore);

  transform(message: LocalizedMessage | null | undefined): string {
    return message ? this.locale.resolve(message) : '';
  }
}
