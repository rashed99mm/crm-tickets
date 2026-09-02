import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { LocaleStore } from './locale.store';

/**
 * Tells the server which language the UI is in.
 *
 * Without this the two halves disagree. `UserContext.Locale` on the backend
 * (`Security/UserContext.cs:46`) reads `Accept-Language`, and a good deal hangs off it: every
 * response message is resolved in that language, and `ResilientAiService` (`Ai/ResilientAiService.cs:35`)
 * picks its **entire prompt** from it — an Arabic locale asks the model for Arabic replies. But
 * nothing was setting the header, so the browser's own `Accept-Language` decided, and the app's
 * language switcher had no effect on the server at all. Switching the portal to Arabic left AI
 * suggestions in English, and an English UI could be served Arabic messages.
 *
 * Same-origin only: the header is meaningful to this platform's API, and a third-party host has no
 * business being told the user's language preference.
 */
export const acceptLanguageInterceptor: HttpInterceptorFn = (req, next) => {
  // A caller that set the header itself has already decided; do not overrule it.
  if (req.headers.has('Accept-Language')) {
    return next(req);
  }

  if (!isSameOrigin(req.url)) {
    return next(req);
  }

  return next(req.clone({ setHeaders: { 'Accept-Language': inject(LocaleStore).locale() } }));
};

/**
 * Relative URLs ("/api/...") are same-origin by definition, which is how every API client in this
 * workspace addresses the backend. An absolute URL is compared against the current origin.
 */
function isSameOrigin(url: string): boolean {
  if (!/^[a-z][a-z\d+\-.]*:/i.test(url) && !url.startsWith('//')) {
    return true;
  }

  try {
    return new URL(url, globalThis.location?.origin).origin === globalThis.location?.origin;
  } catch {
    return false;
  }
}
