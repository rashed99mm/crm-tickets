import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { envelopeInterceptor } from 'common';
import CsatReportComponent from './csat-report.component';

function ok<T>(data: T) {
  return { success: true, code: 'CON035', message: 'OK', data, errors: [] };
}

const SERVER_FAILURE = {
  success: false,
  code: 'INTERNAL_ERROR',
  message: 'Something went wrong on the server',
  data: null,
  errors: [],
};

describe('CsatReportComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: convertToParamMap({}) } },
        },
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  function render(): ComponentFixture<CsatReportComponent> {
    const fixture = TestBed.createComponent(CsatReportComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('renders the average, the NPS-style split and the by-rating table', () => {
    const fixture = render();
    http
      .expectOne((r) => r.url === '/api/reports/csat')
      .flush(
        ok({
          totalResponses: 4,
          averageRating: 4.75,
          promoters: 3,
          passives: 1,
          detractors: 0,
          byRating: [
            { rating: 5, count: 3 },
            { rating: 4, count: 1 },
          ],
        }),
      );
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    // The report endpoint's own average is rendered untouched (4.75), like every other report figure.
    expect(text).toContain('4.75');
    expect(text).toContain('3');
    expect(text).not.toContain('No survey responses in this period.');
  });

  it('renders an empty state without a retry when there are no responses', () => {
    const fixture = render();
    http
      .expectOne((r) => r.url === '/api/reports/csat')
      .flush(
        ok({ totalResponses: 0, averageRating: 0, promoters: 0, passives: 0, detractors: 0, byRating: [] }),
      );
    fixture.detectChanges();

    expect(fixture.componentInstance.state().status).toBe('empty');
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('No survey responses in this period.');
    // An empty state means the request succeeded with no data — there is nothing to retry. The
    // card above carries the date-range filter's Apply button, so the search is scoped to the state.
    expect(el.querySelector('cs-empty-state button')).toBeNull();
    expect(el.querySelector('cs-empty-state [role="alert"]')).toBeNull();
  });

  it('renders the error state with a retry when the request fails', () => {
    const fixture = render();
    http.expectOne((r) => r.url === '/api/reports/csat').flush(SERVER_FAILURE, { status: 500, statusText: 'Error' });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const alert = el.querySelector('cs-error-state [role="alert"]');
    expect(alert?.textContent).toContain('Something went wrong on the server');
    expect(el.querySelector('cs-error-state button')).not.toBeNull();
    expect(el.textContent).not.toContain('No survey responses in this period.');
  });
});