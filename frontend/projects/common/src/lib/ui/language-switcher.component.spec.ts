import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { LocaleStore } from '../i18n/locale.store';
import { CsLanguageSwitcher } from './language-switcher.component';

describe('CsLanguageSwitcher', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
  });

  it('switches the locale and fetches nothing', () => {
    const fixture = TestBed.createComponent(CsLanguageSwitcher);
    fixture.detectChanges();

    const store = TestBed.inject(LocaleStore);
    expect(store.locale()).toBe('en');

    (fixture.nativeElement as HTMLElement).querySelector('button')!.click();
    fixture.detectChanges();

    expect(store.locale()).toBe('ar');
    // Switching language re-renders from data already held (ADR 0007).
    TestBed.inject(HttpTestingController).verify();
  });

  it('labels itself for assistive technology', () => {
    const fixture = TestBed.createComponent(CsLanguageSwitcher);
    fixture.detectChanges();

    const button = (fixture.nativeElement as HTMLElement).querySelector('button')!;
    // The visible label is two characters ("EN" / "ع"), which tells a screen
    // reader nothing about what pressing it does.
    expect(button.getAttribute('aria-label')).toBeTruthy();
  });
});
