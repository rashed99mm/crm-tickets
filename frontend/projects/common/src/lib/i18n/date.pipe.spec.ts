import { TestBed } from '@angular/core/testing';
import { CsDatePipe } from './date.pipe';
import { LocaleStore } from './locale.store';

describe('CsDatePipe', () => {
  let pipe: CsDatePipe;
  let locale: LocaleStore;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [CsDatePipe] });
    pipe = TestBed.inject(CsDatePipe);
    locale = TestBed.inject(LocaleStore);
  });

  /** The defect this pipe exists for: the raw wire value must never reach the screen. */
  it('never renders the raw ISO string', () => {
    const rendered = pipe.transform('2026-08-25T22:01:56.4371Z');

    expect(rendered).not.toContain('T22:01');
    expect(rendered).not.toContain('4371');
    expect(rendered).not.toContain('Z');
    expect(rendered).toContain('2026');
  });

  it('renders a date-only mode without a time', () => {
    const rendered = pipe.transform('2026-08-25T22:01:56.4371Z', 'date');

    expect(rendered).toContain('2026');
    expect(rendered).not.toMatch(/\d{2}:\d{2}/);
  });

  /**
   * The `pure: false` contract. A pure pipe memoises on its argument, so the same instant would
   * keep its English rendering after a switch — the bug its two siblings already had.
   */
  it('re-renders in Arabic after the locale switches', () => {
    const iso = '2026-08-25T22:01:56.4371Z';
    const english = pipe.transform(iso);

    locale.setLocale('ar');
    const arabic = pipe.transform(iso);

    expect(arabic).not.toEqual(english);
  });

  it('returns empty for a missing instant rather than "Invalid Date"', () => {
    expect(pipe.transform(null)).toBe('');
    expect(pipe.transform(undefined)).toBe('');
  });

  /** A malformed instant is a contract problem: surface it, do not swallow it to ''. */
  it('surfaces a malformed instant instead of hiding it', () => {
    expect(pipe.transform('not-a-date')).toBe('not-a-date');
  });
});
