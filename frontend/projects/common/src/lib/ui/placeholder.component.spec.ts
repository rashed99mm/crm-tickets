import { TestBed } from '@angular/core/testing';
import { LocaleStore } from '../i18n/locale.store';
import { TRANSLATIONS } from '../i18n/translations';
import { CsPlaceholder } from './placeholder.component';

function render(field: string): HTMLElement {
  const fixture = TestBed.createComponent(CsPlaceholder);
  fixture.componentRef.setInput('field', field);
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('CsPlaceholder', () => {
  // A fresh injector over a dirty localStorage is not a fresh locale — the store
  // seeds itself from it. Clearing keeps each test's starting language its own.
  beforeEach(() => localStorage.clear());

  it('AC97: renders the dictionary’s not-recorded string', () => {
    expect(render('customers.profile.mrr').textContent?.trim()).toBe(
      TRANSLATIONS['field.notRecorded'].en,
    );
  });

  it('AC97: follows the locale, like every other string in the product', () => {
    TestBed.inject(LocaleStore).setLocale('ar');

    expect(render('customers.profile.mrr').textContent?.trim()).toBe(
      TRANSLATIONS['field.notRecorded'].ar,
    );
  });

  /**
   * The `AC-92` boundary, and the reason this component exists rather than a
   * bare span in fifteen templates.
   *
   * A placeholder marks a field the backend does not supply. The moment it is
   * focusable, or a button, or a link, it stops describing an absence and
   * starts promising a capability the product does not have — which is exactly
   * what `AC-92` forbids. Asserting it here means the rule holds for every
   * unbacked field on every screen, not just the ones someone reviewed.
   */
  it('AC97: is not a control — nothing to focus, nothing to activate', () => {
    const el = render('customers.profile.mrr');

    expect(el.querySelector('button, a, input, select, textarea, [tabindex]')).toBeNull();
    expect(el.querySelector('[role]')).toBeNull();
  });

  /**
   * `field` names the thing that is missing. It is not rendered — the visible
   * text is the same everywhere by design — but it is carried into the DOM so
   * that a reviewer reading rendered HTML, or a future screenshot diff, can
   * tell which absence they are looking at.
   */
  it('AC97: carries the field it stands in for, for the reader of the DOM', () => {
    expect(render('customers.profile.mrr').querySelector('[data-field]')?.getAttribute('data-field'))
      .toBe('customers.profile.mrr');
  });
});
