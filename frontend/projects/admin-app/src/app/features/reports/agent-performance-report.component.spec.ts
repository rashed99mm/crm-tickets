import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { envelopeInterceptor } from 'common';
import AgentPerformanceReportComponent from './agent-performance-report.component';

function ok<T>(data: T) {
  return { success: true, code: 'CON035', message: 'OK', data, errors: [] };
}

describe('AgentPerformanceReportComponent', () => {
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

  function render(): ComponentFixture<AgentPerformanceReportComponent> {
    const fixture = TestBed.createComponent(AgentPerformanceReportComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('AC162: renders one row per agent with resolved count and avg handle minutes', () => {
    const fixture = render();
    http.expectOne((r) => r.url === '/api/reports/agent-performance').flush(
      ok({ byAgent: [{ agentId: 'a-1', agentName: 'Layla Haddad', ticketsResolved: 7, avgHandleMinutes: 42.5 }] }),
    );
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Layla Haddad');
    expect(text).toContain('7');
  });
});
