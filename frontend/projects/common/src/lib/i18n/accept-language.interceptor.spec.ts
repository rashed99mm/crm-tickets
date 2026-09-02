import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { acceptLanguageInterceptor } from './accept-language.interceptor';
import { LocaleStore } from './locale.store';

describe('acceptLanguageInterceptor', () => {
  let http: HttpTestingController;
  let client: HttpClient;
  let locale: LocaleStore;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([acceptLanguageInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    client = TestBed.inject(HttpClient);
    locale = TestBed.inject(LocaleStore);
  });

  afterEach(() => http.verify());

  it('sends the app language, not the browser language, so the server localizes to match the UI', () => {
    locale.setLocale('ar');

    client.get('/api/Tickets').subscribe();

    const req = http.expectOne('/api/Tickets');
    expect(req.request.headers.get('Accept-Language')).toBe('ar');
    req.flush({});
  });

  it('follows a switch back to English', () => {
    locale.setLocale('en');

    client.get('/api/Tickets').subscribe();

    const req = http.expectOne('/api/Tickets');
    expect(req.request.headers.get('Accept-Language')).toBe('en');
    req.flush({});
  });

  it('leaves an explicitly-set Accept-Language alone', () => {
    // A caller that has already decided (an export asking for a fixed language, say) wins.
    locale.setLocale('ar');

    client.get('/api/Reports', { headers: { 'Accept-Language': 'en' } }).subscribe();

    const req = http.expectOne('/api/Reports');
    expect(req.request.headers.get('Accept-Language')).toBe('en');
    req.flush({});
  });

  it('does not touch requests to other origins', () => {
    // Only this platform's API reads the header; a third-party URL should not be told our language.
    locale.setLocale('ar');

    client.get('https://cdn.example.com/asset.json').subscribe();

    const req = http.expectOne('https://cdn.example.com/asset.json');
    expect(req.request.headers.has('Accept-Language')).toBe(false);
    req.flush({});
  });
});
