import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { envelopeInterceptor } from 'common';
import TicketVolumeReportComponent from './ticket-volume-report.component';

function ok<T>(data: T) {
  return { success: true, code: 'CON035', message: 'OK', data, errors: [] };
}

describe('TicketVolumeReportComponent', () => {
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

  function render(): ComponentFixture<TicketVolumeReportComponent> {
    const fixture = TestBed.createComponent(TicketVolumeReportComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('AC160: renders the three breakdowns returned by the api', () => {
    const fixture = render();
    const request = http.expectOne((r) => r.url === '/api/reports/ticket-volume');
    request.flush(
      ok({
        byPeriod: [{ key: '2026-08-27', count: 3 }],
        byCategory: [{ key: 'Technical', count: 3 }],
        byPriority: [{ key: 'Normal', count: 3 }],
      }),
    );
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Technical');
    expect(text).toContain('Normal');
  });

  it('AC160: changing groupBy re-fetches byPeriod', () => {
    const fixture = render();
    http
      .expectOne((r) => r.url === '/api/reports/ticket-volume')
      .flush(ok({ byPeriod: [], byCategory: [], byPriority: [] }));
    fixture.detectChanges();

    fixture.componentInstance.setGroupBy('month');

    const request = http.expectOne((r) => r.url === '/api/reports/ticket-volume');
    expect(request.request.params.get('groupBy')).toBe('month');
    request.flush(ok({ byPeriod: [], byCategory: [], byPriority: [] }));
  });

  it('AC160: hides raw GUID category ids behind a friendly label', () => {
    const fixture = render();
    const guid = '11111111-1111-1111-1111-111111111111';

    http
      .expectOne((r) => r.url === '/api/reports/ticket-volume')
      .flush(ok({ byPeriod: [], byCategory: [{ key: guid, count: 2 }], byPriority: [] }));
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Uncategorized');
    expect(text).not.toContain(guid);
  });
});
