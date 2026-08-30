import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ApiError } from '../api/api-error';
import { CsEmptyState } from './empty-state.component';
import { CsErrorState } from './error-state.component';
import { CsLoadingState } from './loading-state.component';

function anError(): ApiError {
  return new ApiError(
    'ERR900',
    'Something went wrong',
    [],
    '00-abc123',
    500,
  );
}

function networkError(): ApiError {
  return new ApiError(
    'NETWORK_ERROR',
    'Could not reach the server',
    [],
    '',
    0,
  );
}

describe('state components', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
  });

  it('loading announces itself to assistive technology', () => {
    const fixture = TestBed.createComponent(CsLoadingState);
    fixture.detectChanges();

    expect(
      (fixture.nativeElement as HTMLElement).querySelector('[role="status"]'),
    ).not.toBeNull();
  });

  it('empty shows the supplied message', () => {
    const fixture = TestBed.createComponent(CsEmptyState);
    fixture.componentRef.setInput('message', 'No tickets match your filters');
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'No tickets match your filters',
    );
  });

  it('error shows the localised message and the trace id, and offers retry', () => {
    const fixture = TestBed.createComponent(CsErrorState);
    fixture.componentRef.setInput('error', anError());
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Something went wrong');
    // The trace id is what support asks for; hiding it wastes a round trip.
    expect(el.textContent).toContain('00-abc123');
    expect(el.querySelector('button')).not.toBeNull();
  });

  it('modernizes the transport failure copy instead of showing the raw network message', () => {
    const fixture = TestBed.createComponent(CsErrorState);
    fixture.componentRef.setInput('error', networkError());
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Connection interrupted');
    expect(text).toContain('Try again');
    expect(text).not.toContain('Could not reach the server');
  });

  it('keeps the trace id left-to-right inside a right-to-left page', () => {
    // A reversed trace id is unreadable, and it is the one string a user has
    // to read back to support verbatim.
    const fixture = TestBed.createComponent(CsErrorState);
    fixture.componentRef.setInput('error', anError());
    fixture.detectChanges();

    const trace = (fixture.nativeElement as HTMLElement).querySelector(
      '[dir="ltr"]',
    );
    expect(trace?.textContent).toContain('00-abc123');
  });

  it('empty and error render distinguishably', () => {
    // AC-58. If these two look alike, a failure reads as "no data" and the
    // real fault never gets reported.
    const emptyFixture = TestBed.createComponent(CsEmptyState);
    emptyFixture.componentRef.setInput('message', 'Nothing here');
    emptyFixture.detectChanges();

    const errorFixture = TestBed.createComponent(CsErrorState);
    errorFixture.componentRef.setInput('error', anError());
    errorFixture.detectChanges();

    const emptyEl = emptyFixture.nativeElement as HTMLElement;
    const errorEl = errorFixture.nativeElement as HTMLElement;

    expect(emptyEl.innerHTML).not.toBe(errorEl.innerHTML);
    // The retry button is the concrete difference, not just styling.
    expect(errorEl.querySelector('button')).not.toBeNull();
    expect(emptyEl.querySelector('button')).toBeNull();
  });
});
