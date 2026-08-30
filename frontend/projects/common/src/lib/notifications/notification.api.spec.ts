import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { envelopeInterceptor } from '../api/envelope.interceptor';
import { NotificationApi } from './notification.api';

describe('NotificationApi', () => {
  let api: NotificationApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    api = TestBed.inject(NotificationApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists notifications with paging', () => {
    api.list(2, 50).subscribe();

    const request = http.expectOne((r) => r.url === '/api/Notifications');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('50');
    request.flush({
      success: true,
      code: 'CON035',
      message: 'OK',
      data: { items: [], pageIndex: 2, pageSize: 50, totalCount: 0 },
      errors: [],
    });
  });

  it('posts a mark-read command to the current notification endpoint', () => {
    api.markRead('n-1').subscribe();

    const request = http.expectOne('/api/Notifications/n-1/read');
    expect(request.request.method).toBe('POST');
    request.flush(null);
  });

  it('gets the unread count', () => {
    let count = 0;
    api.unreadCount().subscribe((value) => (count = value));

    const request = http.expectOne('/api/Notifications/unread/count');
    expect(request.request.method).toBe('GET');
    request.flush({ success: true, code: 'CON035', message: 'OK', data: 3, errors: [] });

    expect(count).toBe(3);
  });
});
