import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { envelopeInterceptor, MAX_ATTACHMENT_BYTES } from 'common';
import { vi } from 'vitest';
import { CustomerAttachmentsComponent } from './customer-attachments.component';

const ATTACHMENTS = [
  {
    id: 'a-2',
    originalFileName: 'invoice.pdf',
    contentType: 'application/pdf',
    sizeBytes: 2_411_724,
    uploadedByName: 'Dana Support',
    createdAt: '2026-08-26T11:00:00.000Z',
  },
  {
    id: 'a-1',
    originalFileName: 'screenshot.png',
    contentType: 'image/png',
    sizeBytes: 51_200,
    uploadedByName: 'Omar Agent',
    createdAt: '2026-08-26T09:00:00.000Z',
  },
];

function attachmentsPage(items: unknown[], totalCount = items.length) {
  return {
    success: true,
    code: 'CON035',
    message: 'OK',
    data: { items, pageIndex: 1, pageSize: 20, totalCount },
    errors: [],
  };
}

const SERVER_FAILURE = {
  success: false,
  code: 'INTERNAL_ERROR',
  message: 'The attachments could not be loaded',
  data: null,
  errors: [],
};

/** What the server sends when it refuses a file this client happened to accept (`AC-24`). */
const UNSUPPORTED_TYPE_ENVELOPE = {
  success: false,
  code: 'ATTACHMENT_TYPE_NOT_ALLOWED',
  message: 'That file type is not accepted',
  data: null,
  errors: [],
};

/**
 * A file that *reports* a size without allocating one. Ten megabytes of real bytes would make the
 * suite slower for no extra confidence — the component reads `File.size` and nothing else.
 */
function fileOfSize(name: string, type: string, size: number): File {
  const file = new File(['x'], name, { type });
  Object.defineProperty(file, 'size', { value: size });
  return file;
}

/** The shape `chooseFile` actually reads, without fighting jsdom over a read-only `FileList`. */
function changeEventFor(input: { files: File[]; value: string }): Event {
  return { target: input } as unknown as Event;
}

describe('CustomerAttachmentsComponent', () => {
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

  async function render(): Promise<ComponentFixture<CustomerAttachmentsComponent>> {
    const fixture = TestBed.createComponent(CustomerAttachmentsComponent);
    fixture.componentRef.setInput('customerId', 'c-1');
    fixture.detectChanges();

    // The load is queued on a microtask so the bound id is in place before it fires.
    await Promise.resolve();
    return fixture;
  }

  function flushList(
    fixture: ComponentFixture<CustomerAttachmentsComponent>,
    body: object,
    status?: number,
  ) {
    const request = http.expectOne(
      (r) => r.method === 'GET' && r.url === '/api/Customers/c-1/attachments',
    );
    if (status) {
      request.flush(body, { status, statusText: 'Error' });
    } else {
      request.flush(body);
    }
    fixture.detectChanges();
    return request;
  }

  it('AC83: lists attachments with name, size and type', async () => {
    const fixture = await render();
    flushList(fixture, attachmentsPage(ATTACHMENTS));

    const rows = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('[data-testid="attachment-list"] li'),
    ).map((li) => li.textContent ?? '');

    expect(rows).toHaveLength(2);

    // The original filename — the name the agent recognises, not the server-generated stored one.
    expect(rows[0]).toContain('invoice.pdf');
    // A size a human can judge, not the raw byte count.
    expect(rows[0]).toContain('2.3 MB');
    expect(rows[0]).not.toContain('2411724');
    expect(rows[0]).toContain('application/pdf');

    expect(rows[1]).toContain('screenshot.png');
    // The criterion is a human-readable size, not a particular decimal count: formatBytes shows
    // one decimal below 10 and none above ("9.4 MB" is useful, "94.3 MB" is noise), so 50 KB
    // renders whole. Asserting the raw byte count is absent is the part that matters.
    expect(rows[1]).toContain('50 KB');
    expect(rows[1]).not.toContain('51200');
    expect(rows[1]).toContain('image/png');
  });

  it('AC83: a failed load renders the error state, not an empty list', async () => {
    const fixture = await render();
    flushList(fixture, SERVER_FAILURE, 500);

    expect(fixture.componentInstance.state().status).toBe('error');

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('The attachments could not be loaded');
    // The distinction the AsyncState union exists to preserve: an outage is not "no files".
    expect(el.textContent).not.toContain('No files attached to this customer yet');
    expect(el.querySelector('[data-testid="attachment-list"]')).toBeNull();
  });

  it('AC83: a customer with no attachments gets the empty state', async () => {
    const fixture = await render();
    flushList(fixture, attachmentsPage([]));

    expect(fixture.componentInstance.state().status).toBe('empty');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'No files attached to this customer yet',
    );
  });

  /**
   * The criterion is that the client refuses **before** spending the upload, so the assertion is
   * `expectNone`. A component that sent a 10 MB file and rendered the server's 413 would satisfy
   * every visual assertion while failing the requirement outright — the round trip is the cost the
   * check exists to avoid.
   */
  it('AC84: a file over the size limit is refused without a request', async () => {
    const fixture = await render();
    flushList(fixture, attachmentsPage(ATTACHMENTS));

    fixture.componentInstance.upload(fileOfSize('huge.png', 'image/png', MAX_ATTACHMENT_BYTES + 1));
    fixture.detectChanges();

    http.expectNone('/api/Customers/c-1/attachments');

    // And the refusal names which rule refused it, because "too large" and "wrong type" need
    // different corrections from the user.
    const refusal = (fixture.nativeElement as HTMLElement).querySelector(
      '[data-testid="attachment-refusal"]',
    );
    expect(refusal?.textContent).toContain('huge.png');
    expect(refusal?.textContent).toContain('The limit is 10 MB');
  });

  it('AC84: a disallowed type is refused without a request', async () => {
    const fixture = await render();
    flushList(fixture, attachmentsPage(ATTACHMENTS));

    fixture.componentInstance.upload(
      new File(['MZ'], 'installer.exe', { type: 'application/x-msdownload' }),
    );
    fixture.detectChanges();

    http.expectNone('/api/Customers/c-1/attachments');

    const refusal = (fixture.nativeElement as HTMLElement).querySelector(
      '[data-testid="attachment-refusal"]',
    );
    expect(refusal?.textContent).toContain('installer.exe');
    expect(refusal?.textContent).toContain('Accepted: PNG, JPEG, GIF, PDF or plain text');
  });

  it('AC84: a valid file uploads as multipart and the list re-reads', async () => {
    const fixture = await render();
    flushList(fixture, attachmentsPage([ATTACHMENTS[1]]));

    fixture.componentInstance.upload(
      new File(['%PDF-1.7'], 'invoice.pdf', { type: 'application/pdf' }),
    );
    fixture.detectChanges();

    const post = http.expectOne(
      (r) => r.method === 'POST' && r.url === '/api/Customers/c-1/attachments',
    );
    expect(post.request.body).toBeInstanceOf(FormData);
    // The browser must own the multipart boundary; a hand-set header has none and cannot be parsed.
    expect(post.request.headers.has('Content-Type')).toBe(false);
    // Feedback while it is in flight, so a slow upload does not read as a hang.
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Uploading…');

    post.flush({ success: true, code: 'CON035', message: 'OK', data: { id: 'a-2' }, errors: [] });
    fixture.detectChanges();

    // AC-84 — the file appears without a page reload, because the list is re-read from the server
    // rather than having a row invented locally.
    const reread = http.expectOne(
      (r) => r.method === 'GET' && r.url === '/api/Customers/c-1/attachments',
    );
    reread.flush(attachmentsPage(ATTACHMENTS));
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('invoice.pdf');
    expect(fixture.componentInstance.uploading()).toBe(false);
  });

  /**
   * The two checks are independent by design. This is what the screen does when they disagree —
   * the server's message, not the client's guess.
   */
  it('AC84: the server refusing a file the client accepted is shown as the server said it', async () => {
    const fixture = await render();
    flushList(fixture, attachmentsPage(ATTACHMENTS));

    fixture.componentInstance.upload(new File(['note'], 'notes.txt', { type: 'text/plain' }));
    http
      .expectOne((r) => r.method === 'POST')
      .flush(UNSUPPORTED_TYPE_ENVELOPE, {
        status: 415,
        statusText: 'Unsupported Media Type',
      });
    fixture.detectChanges();

    expect(
      (fixture.nativeElement as HTMLElement).querySelector(
        '[data-testid="attachment-upload-error"]',
      )?.textContent,
    ).toContain('That file type is not accepted');
  });

  /**
   * Picking the same file twice must still fire a `change` event, or a retry after a failed upload
   * silently does nothing. Clearing the input's value is what makes the second pick a change.
   */
  it('AC84: choosing a file clears the input so the same file can be picked again', async () => {
    const fixture = await render();
    flushList(fixture, attachmentsPage(ATTACHMENTS));

    const input = {
      files: [new File(['x'], 'a.png', { type: 'image/png' })],
      value: 'C:\\fake\\a.png',
    };
    fixture.componentInstance.chooseFile(changeEventFor(input));

    expect(input.value).toBe('');
    http
      .expectOne((r) => r.method === 'POST')
      .flush({
        success: true,
        code: 'CON035',
        message: 'OK',
        data: { id: 'a-3' },
        errors: [],
      });
    fixture.detectChanges();
    http.expectOne((r) => r.method === 'GET').flush(attachmentsPage(ATTACHMENTS));
  });

  /**
   * AC-85 — the bytes are fetched through `HttpClient` so the auth interceptor signs the request,
   * then handed to the browser as an object URL. A plain `<a href>` would carry no `Authorization`
   * header, 401, and read as a broken button — so this asserts the row has no such link.
   */
  it('AC85: downloading fetches the blob through HttpClient rather than linking to the route', async () => {
    const objectUrl = 'blob:mock-url';
    const createObjectURL = vi.fn(() => objectUrl);
    const revokeObjectURL = vi.fn();
    URL.createObjectURL = createObjectURL as unknown as typeof URL.createObjectURL;
    URL.revokeObjectURL = revokeObjectURL as unknown as typeof URL.revokeObjectURL;
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});

    const fixture = await render();
    flushList(fixture, attachmentsPage(ATTACHMENTS));

    // No anchor points at the protected content route; the download is a button.
    expect(
      (fixture.nativeElement as HTMLElement).querySelector('a[href*="/attachments/"]'),
    ).toBeNull();

    fixture.componentInstance.download(ATTACHMENTS[0]);
    const request = http.expectOne('/api/Customers/c-1/attachments/a-2/content');
    expect(request.request.method).toBe('GET');
    expect(request.request.responseType).toBe('blob');
    request.flush(new Blob(['%PDF'], { type: 'application/pdf' }));
    fixture.detectChanges();

    expect(createObjectURL).toHaveBeenCalledOnce();
    expect(click).toHaveBeenCalledOnce();
    const anchor = click.mock.instances[0] as HTMLAnchorElement;
    expect(anchor.download).toBe('invoice.pdf');

    // Revoked on the next macrotask — synchronously can cancel a download that has not started,
    // and never revoking leaks the blob for the life of a page an agent keeps open all day.
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(revokeObjectURL).toHaveBeenCalledWith(objectUrl);

    click.mockRestore();
  });

  it('AC85: removing an attachment asks first and sends nothing until it is confirmed', async () => {
    const fixture = await render();
    flushList(fixture, attachmentsPage(ATTACHMENTS));

    fixture.componentInstance.askToRemove('a-2');
    fixture.detectChanges();

    expect(
      (fixture.nativeElement as HTMLElement).querySelector(
        '[data-testid="attachment-remove-confirm"]',
      ),
    ).not.toBeNull();
    // Asking is not doing: nothing has been deleted yet.
    http.expectNone('/api/Customers/c-1/attachments/a-2');
  });

  it('AC85: removing an attachment re-reads the list', async () => {
    const fixture = await render();
    flushList(fixture, attachmentsPage(ATTACHMENTS));

    fixture.componentInstance.askToRemove('a-2');
    fixture.componentInstance.confirmRemove('a-2');

    const remove = http.expectOne('/api/Customers/c-1/attachments/a-2');
    expect(remove.request.method).toBe('DELETE');
    remove.flush({ success: true, code: 'CON035', message: 'OK', data: null, errors: [] });
    fixture.detectChanges();

    // Re-read rather than spliced out locally, so the screen shows what the server still holds.
    const reread = http.expectOne(
      (r) => r.method === 'GET' && r.url === '/api/Customers/c-1/attachments',
    );
    reread.flush(attachmentsPage([ATTACHMENTS[1]]));
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).not.toContain('invoice.pdf');
    expect(el.textContent).toContain('screenshot.png');
    expect(fixture.componentInstance.confirmingRemovalOf()).toBeNull();
  });

  it('AC85: a refused removal keeps the file on screen and shows why', async () => {
    const fixture = await render();
    flushList(fixture, attachmentsPage(ATTACHMENTS));

    fixture.componentInstance.askToRemove('a-2');
    fixture.componentInstance.confirmRemove('a-2');

    http.expectOne('/api/Customers/c-1/attachments/a-2').flush(
      {
        success: false,
        code: 'ATTACHMENT_NOT_FOUND',
        message: 'That attachment no longer exists',
        data: null,
        errors: [],
      },
      { status: 404, statusText: 'Not Found' },
    );
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="attachment-remove-error"]')?.textContent).toContain(
      'That attachment no longer exists',
    );
    // The row stays: removing it would suggest a deletion that did not happen.
    expect(el.textContent).toContain('invoice.pdf');
  });
});
