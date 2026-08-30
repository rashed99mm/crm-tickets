import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { envelopeInterceptor } from '../api/envelope.interceptor';
import { CustomerApi } from './customer.api';

describe('CustomerApi', () => {
  let api: CustomerApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    api = TestBed.inject(CustomerApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends paging and search as query parameters', () => {
    api.list({ page: 3, pageSize: 25, search: 'layla' }).subscribe();

    const request = http.expectOne((r) => r.url === '/api/Customers');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('page')).toBe('3');
    expect(request.request.params.get('pageSize')).toBe('25');
    expect(request.request.params.get('search')).toBe('layla');
    request.flush({
      success: true,
      code: 'CON035',
      message: 'OK',
      data: { items: [], pageIndex: 3, pageSize: 25, totalCount: 0 },
      errors: [],
    });
  });

  /**
   * A blank `search=` is not the same as no search. The backend treats an empty string as "no
   * filter" today, but sending one makes the request URL lie about what was asked for, and it is
   * the kind of difference a future server-side `NotEmpty` rule turns into a 400.
   */
  it('omits an unset search rather than sending an empty one', () => {
    api.list({}).subscribe();

    const request = http.expectOne((r) => r.url === '/api/Customers');
    expect(request.request.params.has('search')).toBe(false);
    request.flush({
      success: true,
      code: 'CON035',
      message: 'OK',
      data: { items: [], pageIndex: 1, pageSize: 10, totalCount: 0 },
      errors: [],
    });
  });

  it('unwraps the envelope so callers see the page, not the envelope', () => {
    let received: unknown;
    api.list({}).subscribe((page) => (received = page));

    http.expectOne((r) => r.url === '/api/Customers').flush({
      success: true,
      code: 'CON035',
      message: 'OK',
      data: { items: [{ id: 'c-1' }], pageIndex: 1, pageSize: 10, totalCount: 1 },
      errors: [],
    });

    expect(received).toEqual({ items: [{ id: 'c-1' }], pageIndex: 1, pageSize: 10, totalCount: 1 });
  });

  it('reads one customer from /api/Customers/{id}', () => {
    api.get('c-1').subscribe();

    const request = http.expectOne('/api/Customers/c-1');
    expect(request.request.method).toBe('GET');
    request.flush({ success: true, code: 'CON035', message: 'OK', data: { id: 'c-1' }, errors: [] });
  });

  it('posts a create with the payload the backend expects', () => {
    api.create({ name: 'Layla Haddad', email: 'layla@example.com', phone: '+20 100' }).subscribe();

    const request = http.expectOne('/api/Customers');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      name: 'Layla Haddad',
      email: 'layla@example.com',
      phone: '+20 100',
    });
    request.flush({ success: true, code: 'CON035', message: 'OK', data: { id: 'c-1' }, errors: [] });
  });

  it('puts an update to /api/Customers/{id}', () => {
    api.update('c-1', { name: 'Layla H', email: 'layla@example.com', phone: null }).subscribe();

    const request = http.expectOne('/api/Customers/c-1');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({
      name: 'Layla H',
      email: 'layla@example.com',
      phone: null,
    });
    request.flush({ success: true, code: 'CON035', message: 'OK', data: { id: 'c-1' }, errors: [] });
  });

  it('deletes /api/Customers/{id}', () => {
    api.remove('c-1').subscribe();

    const request = http.expectOne('/api/Customers/c-1');
    expect(request.request.method).toBe('DELETE');
    request.flush({ success: true, code: 'CON035', message: 'OK', data: null, errors: [] });
  });

  it('AC74: reads notes from the customer-scoped notes route, paged', () => {
    api.listNotes('c-1', 2, 20).subscribe();

    const request = http.expectOne((r) => r.url === '/api/Customers/c-1/notes');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('20');
    request.flush({
      success: true,
      code: 'CON035',
      message: 'OK',
      data: { items: [], pageIndex: 2, pageSize: 20, totalCount: 0 },
      errors: [],
    });
  });

  /**
   * AC-76 is a server criterion — the author comes from the token. This is the client half of it:
   * `addNote` takes only a body, so there is no parameter through which a caller could name an
   * author even by mistake. The assertion is on the WHOLE body, not on the absence of one key,
   * because a signature change that added an author would otherwise slip through.
   */
  it('AC76: posting a note sends only the body — the client has no author to send', () => {
    api.addNote('c-1', 'Called back, awaiting logs.').subscribe();

    const request = http.expectOne('/api/Customers/c-1/notes');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ body: 'Called back, awaiting logs.' });
    request.flush({ success: true, code: 'CON035', message: 'OK', data: { id: 'n-1' }, errors: [] });
  });

  it('AC83: reads attachments from the customer-scoped route, paged', () => {
    api.listAttachments('c-1', 2, 20).subscribe();

    const request = http.expectOne((r) => r.url === '/api/Customers/c-1/attachments');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('20');
    request.flush({
      success: true,
      code: 'CON035',
      message: 'OK',
      data: { items: [], pageIndex: 2, pageSize: 20, totalCount: 0 },
      errors: [],
    });
  });

  /**
   * The `Content-Type` assertion is the point of this test, not decoration.
   *
   * Multipart bodies carry a boundary token the browser generates when it serialises the
   * `FormData`. Setting `Content-Type: multipart/form-data` by hand emits it *without* a boundary,
   * and the server then cannot parse a body that is otherwise correct — it surfaces as a 400 that
   * looks like a validation defect. `detectContentTypeHeader()` returning null is Angular's own
   * signal that it will leave the header to the browser.
   */
  it('AC84: uploading sends the file as multipart and sets no Content-Type header', () => {
    const file = new File(['screenshot bytes'], 'screenshot.png', { type: 'image/png' });
    api.uploadAttachment('c-1', file).subscribe();

    const request = http.expectOne('/api/Customers/c-1/attachments');
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.has('Content-Type')).toBe(false);
    expect(request.request.detectContentTypeHeader()).toBeNull();

    const body = request.request.body as FormData;
    expect(body).toBeInstanceOf(FormData);
    // `append` with a filename re-wraps the blob per spec, so identity is not the assertion —
    // what matters is that the name, type and bytes survive the trip into the form.
    const sent = body.get('file') as File;
    expect(sent.name).toBe('screenshot.png');
    expect(sent.type).toBe('image/png');
    expect(sent.size).toBe(file.size);
    // AC-22's client half — the uploader comes from the token, so there is no field for one.
    expect(Array.from(body.keys())).toEqual(['file']);

    request.flush({ success: true, code: 'CON035', message: 'OK', data: { id: 'a-1' }, errors: [] });
  });

  /**
   * AC-85/AC-26 — the bytes come back through `HttpClient` so the auth interceptor can sign the
   * request. A plain `<a href>` carries no `Authorization` header and would 401.
   */
  it('AC85: downloads the content as a blob through HttpClient, not as a bare link', () => {
    let received: Blob | undefined;
    api.downloadAttachment('c-1', 'a-1').subscribe((blob) => (received = blob));

    const request = http.expectOne('/api/Customers/c-1/attachments/a-1/content');
    expect(request.request.method).toBe('GET');
    expect(request.request.responseType).toBe('blob');

    const bytes = new Blob(['file bytes'], { type: 'image/png' });
    request.flush(bytes);

    // Not an envelope, so the interceptor leaves it alone rather than unwrapping a `data` that
    // does not exist.
    expect(received).toBe(bytes);
  });

  it('AC85: deletes an attachment from the customer-scoped route', () => {
    api.removeAttachment('c-1', 'a-1').subscribe();

    const request = http.expectOne('/api/Customers/c-1/attachments/a-1');
    expect(request.request.method).toBe('DELETE');
    request.flush({ success: true, code: 'CON035', message: 'OK', data: null, errors: [] });
  });
});
