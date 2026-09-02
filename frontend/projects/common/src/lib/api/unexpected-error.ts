import { ApiError } from './api-error';
import { LocaleStore } from '../i18n/locale.store';

/**
 * Normalizes an unknown thrown value into an `ApiError` with a **localized** message.
 *
 * Every HTTP failure already arrives as an `ApiError` — `envelopeInterceptor` guarantees it, using
 * the server's own message, which is resolved in the caller's language now that
 * `acceptLanguageInterceptor` sends one. This helper covers the remaining case: something threw
 * that never reached the server, so there is no server message to show and the client has to
 * supply one.
 *
 * It exists because a dozen components each wrote `new ApiError('ERR_UNKNOWN', 'Something went
 * wrong', [], '', 0)` inline. That literal is English, and on any template rendering
 * `ApiError.message_` directly it reached the user untranslated.
 *
 * `status: 0` is kept deliberately: `cs-error-state` treats a zero status as "could not reach the
 * server" and shows its own localized copy, so components using that component were already
 * covered — this makes the ones rendering the message themselves correct too.
 */
export function toLocalizedApiError(failure: unknown, locale: LocaleStore): ApiError {
  return failure instanceof ApiError
    ? failure
    : new ApiError('ERR_UNKNOWN', locale.t('error.unexpected'), [], '', 0);
}
