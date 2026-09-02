import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { envelopeInterceptor, LocaleStore } from 'common';
import AuditLogComponent from './audit-log.component';

function envelope(data: unknown) {
  return { success: true, code: 'CON035', message: 'OK', data, errors: [] };
}

describe('AuditLogComponent', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  function render(): ComponentFixture<AuditLogComponent> {
    const fixture = TestBed.createComponent(AuditLogComponent);
    fixture.detectChanges();
    http.expectOne((r) => r.url === '/api/admin/audit-log').flush(
      envelope({ items: [], pageIndex: 1, pageSize: 20, totalCount: 0 }),
    );
    fixture.detectChanges();
    return fixture;
  }

  it('AuditLog_ActionAndEntityLabelsAreLocalized_NotRawServerValues', () => {
    const fixture = render();
    const component = fixture.componentInstance;

    expect(component.actionLabel('Created')).toBe('Created');
    expect(component.entityLabel('PlatformSetting')).toBe('Platform Setting');

    TestBed.inject(LocaleStore).setLocale('ar');
    expect(component.actionLabel('Created')).toBe('تم الإنشاء');
    expect(component.entityLabel('PlatformSetting')).toBe('إعداد المنصة');
    TestBed.inject(LocaleStore).setLocale('en');
  });

  it('AuditLog_UnrecognizedActionOrEntityFallsBackToTheRawValue', () => {
    const fixture = render();
    const component = fixture.componentInstance;

    expect(component.actionLabel('SomeFutureAction')).toBe('SomeFutureAction');
    expect(component.entityLabel('SomeFutureEntity')).toBe('SomeFutureEntity');
  });

  it('AuditLog_DetailFieldsNeverRendersRawJson: the payload is a labeled list, not a blob', () => {
    const fixture = render();
    const component = fixture.componentInstance;

    const fields = component.detailFields(
      '{"Email":"a@test.local","Username":"ann","Password":"***REDACTED***","Roles":["Admin","Agent"],"PhoneNumber":null}',
    );

    expect(fields).toEqual([
      { label: 'Email', value: 'a@test.local' },
      { label: 'Username', value: 'ann' },
      { label: 'Password', value: 'Hidden for security' },
      { label: 'Roles', value: 'Admin, Agent' },
      { label: 'Phone number', value: '—' },
    ]);
  });

  it('AuditLog_DetailFieldsHumanizesAnUnmappedFieldName', () => {
    const fixture = render();
    const fields = fixture.componentInstance.detailFields('{"ProfileImageUrl":"https://example.com/a.png"}');

    expect(fields).toEqual([{ label: 'Profile Image Url', value: 'https://example.com/a.png' }]);
  });

  it('AuditLog_DetailFieldsOnNullPayloadIsAnEmptyList', () => {
    const fixture = render();
    expect(fixture.componentInstance.detailFields(null)).toEqual([]);
  });

  it('AuditLog_RedactedValueIsNeverShownAsTheRawPlaceholderString', () => {
    const fixture = render();
    const fields = fixture.componentInstance.detailFields('{"Password":"***REDACTED***"}');

    expect(fields[0].value).not.toContain('REDACTED');
    expect(fields[0].value).toBe('Hidden for security');
  });
});
