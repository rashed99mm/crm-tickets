import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { envelopeInterceptor } from 'common';
import SlaPerformanceReportComponent from './sla-performance-report.component';

function ok<T>(data: T) {
  return { success: true, code: 'CON035', message: 'OK', data, errors: [] };
}

describe('SlaPerformanceReportComponent', () => {
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

  function render(): ComponentFixture<SlaPerformanceReportComponent> {
    const fixture = TestBed.createComponent(SlaPerformanceReportComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('AC161: renders one row per priority with met/breached counts', () => {
    const fixture = render();
    http.expectOne((r) => r.url === '/api/reports/sla-performance').flush(
      ok({
        byPriority: [
          {
            priority: 'High',
            total: 5,
            metFirstResponse: 4,
            breachedFirstResponse: 1,
            metResolution: 3,
            breachedResolution: 2,
          },
        ],
      }),
    );
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('High');
    expect(text).toContain('4');
    expect(text).toContain('1');
  });
});
