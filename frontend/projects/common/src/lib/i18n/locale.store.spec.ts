import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { LocaleStore } from './locale.store';

describe('LocaleStore', () => {
  let store: LocaleStore;

  /** Flushes the effect that writes lang/dir onto the document element. */
  function flushEffects(): void {
    TestBed.inject(ApplicationRef).tick();
  }

  beforeEach(() => {
    localStorage.clear();
    document.documentElement.removeAttribute('dir');
    document.documentElement.removeAttribute('lang');

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(LocaleStore);
  });

  it('defaults to English, left to right', () => {
    expect(store.locale()).toBe('en');
    expect(store.direction()).toBe('ltr');
  });

  it('sets lang and dir on the document element', () => {
    store.setLocale('ar');
    flushEffects();

    expect(document.documentElement.lang).toBe('ar');
    expect(document.documentElement.dir).toBe('rtl');
    expect(store.direction()).toBe('rtl');
  });

  it('resolves a bilingual message to the active locale', () => {
    const message = { ar: 'العميل غير موجود', en: 'Customer not found' };

    expect(store.resolve(message)).toBe('Customer not found');

    store.setLocale('ar');
    expect(store.resolve(message)).toBe('العميل غير موجود');
  });

  it('issues NO http request when the locale changes', () => {
    // FE-7. This is the entire reason the backend sends both languages in
    // every response (ADR 0007). A well-meaning refactor to "reload with the
    // new locale" would quietly undo that design, and nothing else would
    // catch it — the app would still work, just slower and with a flash.
    const mock = TestBed.inject(HttpTestingController);

    store.setLocale('ar');
    store.toggle();
    flushEffects();

    mock.verify(); // throws if anything was requested
    expect(store.locale()).toBe('en');
  });

  it('remembers the choice across a reload', () => {
    store.setLocale('ar');
    flushEffects();

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    expect(TestBed.inject(LocaleStore).locale()).toBe('ar');
  });
});
