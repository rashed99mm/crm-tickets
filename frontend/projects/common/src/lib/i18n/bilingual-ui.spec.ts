import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ApiError } from '../api/api-error';
import { LocalizedMessage } from '../api/api-response';
import { CsErrorState } from '../ui/error-state.component';
import { LocaleStore } from './locale.store';
import { LocalizePipe } from './localize.pipe';
import { TranslatePipe } from './translate.pipe';
import { TRANSLATIONS, TranslationKey } from './translations';

/** A host for the pipe, so the assertion is about a rendered template rather than about t(). */
@Component({
  selector: 'cs-translate-host',
  imports: [TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<p>{{ 'tickets.queue.title' | t }}</p>`,
})
class TranslateHost {}

/** The same, for the server-message pipe. Nothing in the apps used it, so nothing had checked it. */
@Component({
  selector: 'cs-localize-host',
  imports: [LocalizePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<p>{{ message | localize }}</p>`,
})
class LocalizeHost {
  readonly message: LocalizedMessage = { ar: 'تم الحفظ', en: 'Saved' };
}

function anError(): ApiError {
  return new ApiError(
    'TCK004',
    'Ticket not found',
    [],
    '00-abc123',
    404,
  );
}

describe('MVP-13 bilingual UI', () => {
  function flush(): void {
    TestBed.inject(ApplicationRef).tick();
  }

  beforeEach(() => {
    localStorage.clear();
    document.documentElement.removeAttribute('dir');
    document.documentElement.removeAttribute('lang');

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
  });

  // ---- AC-63 ------------------------------------------------------------

  it('AC63: every dictionary entry carries both languages', () => {
    // A half-filled entry renders as `undefined` on screen, which reads as a rendering fault
    // rather than as a missing translation. `as const satisfies` enforces the shape at compile
    // time; this catches the empty string it cannot see.
    const incomplete = (Object.keys(TRANSLATIONS) as TranslationKey[]).filter((key) => {
      const entry = TRANSLATIONS[key];
      return entry.en.trim() === '' || entry.ar.trim() === '';
    });

    expect(incomplete).toEqual([]);
  });

  it('AC63: the translate pipe re-renders text on switch', () => {
    const fixture = TestBed.createComponent(TranslateHost);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent?.trim()).toBe(TRANSLATIONS['tickets.queue.title'].en);

    TestBed.inject(LocaleStore).setLocale('ar');
    flush();

    expect(el.textContent?.trim()).toBe(TRANSLATIONS['tickets.queue.title'].ar);
  });

  it('AC68: the localize pipe re-renders a server message on switch', () => {
    // Its sibling's failure exposed the same defect here: a pure pipe is memoised on its argument,
    // so the same message object would have returned English forever.
    const fixture = TestBed.createComponent(LocalizeHost);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent?.trim()).toBe('Saved');

    TestBed.inject(LocaleStore).setLocale('ar');
    flush();

    expect(el.textContent?.trim()).toBe('تم الحفظ');
    TestBed.inject(HttpTestingController).verify();
  });

  it('AC63: switching to Arabic sets dir=rtl on the document', () => {
    const store = TestBed.inject(LocaleStore);

    store.setLocale('ar');
    flush();

    expect(document.documentElement.dir).toBe('rtl');
    expect(document.documentElement.lang).toBe('ar');

    store.setLocale('en');
    flush();

    // Both directions, not just the interesting one: a switch that goes to rtl and stays there is
    // the same bug in the other direction.
    expect(document.documentElement.dir).toBe('ltr');
  });

  it('AC63: the language choice survives a reload', () => {
    TestBed.inject(LocaleStore).setLocale('ar');
    flush();

    // A fresh injector over the same localStorage is what a reload actually is.
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    expect(TestBed.inject(LocaleStore).locale()).toBe('ar');
  });

  it('AC63: a parameterised string keeps its value in both languages', () => {
    const store = TestBed.inject(LocaleStore);

    expect(store.t('tickets.pageSummary', 2, 37)).toBe('Page 2 — 37 ticket(s)');

    store.setLocale('ar');
    expect(store.t('tickets.pageSummary', 2, 37)).toContain('2');
    expect(store.t('tickets.pageSummary', 2, 37)).toContain('37');
    // The Arabic half must be a different sentence, not the English one copied across.
    expect(store.t('tickets.pageSummary', 2, 37)).not.toContain('Page');
  });

  // ---- AC-68 ------------------------------------------------------------

  it('AC68: switching language issues no HTTP request', () => {
    // The client-owned retry label flips without a refetch — server messages are plain strings
    // resolved by the backend's Accept-Language header, not by the frontend.
    const mock = TestBed.inject(HttpTestingController);
    const store = TestBed.inject(LocaleStore);

    const fixture = TestBed.createComponent(CsErrorState);
    fixture.componentRef.setInput('error', anError());
    fixture.detectChanges();

    store.setLocale('ar');
    store.toggle();
    store.setLocale('ar');
    flush();

    mock.verify(); // throws if any request went out
  });

  it('AC68: error state renders the server message and offers retry', () => {
    const fixture = TestBed.createComponent(CsErrorState);
    fixture.componentRef.setInput('error', anError());
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    // The message is a plain string from the backend — displayed directly.
    expect(el.textContent).toContain('Ticket not found');
    // The client-owned retry label is still bilingual.
    expect(el.textContent).toContain(TRANSLATIONS['action.retry'].en);

    TestBed.inject(HttpTestingController).verify();
  });
});
