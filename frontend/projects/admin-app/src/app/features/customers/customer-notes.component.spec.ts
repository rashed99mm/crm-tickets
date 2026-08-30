import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { envelopeInterceptor } from 'common';
import { CustomerNotesComponent } from './customer-notes.component';

/**
 * The order the server returns: newest first. The fixture deliberately lists the LATER note first
 * so a component that re-sorted by `createdAt` ascending would visibly fail.
 */
const NOTES = [
  {
    id: 'n-2',
    body: 'Called back, awaiting logs.',
    authorId: 'u-1',
    authorName: 'Dana Support',
    createdAt: '2026-08-26T11:00:00.000Z',
  },
  {
    id: 'n-1',
    body: 'Customer reported the portal rejects their password.',
    authorId: 'u-2',
    authorName: 'Omar Agent',
    createdAt: '2026-08-26T09:00:00.000Z',
  },
];

function notesPage(items: unknown[], totalCount = items.length) {
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
  message: 'The notes could not be loaded',
  data: null,
  errors: [],
};

/** The server keys a rejected note to `Body`; the interceptor lowercases it to `body`. */
const BODY_REQUIRED_ENVELOPE = {
  success: false,
  code: 'VALIDATION_ERROR',
  message: 'Validation failed',
  data: null,
  errors: [{ field: 'body', code: 'VALIDATION_ERROR', message: 'A note must have a body' }],
};

describe('CustomerNotesComponent', () => {
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

  async function render(): Promise<ComponentFixture<CustomerNotesComponent>> {
    const fixture = TestBed.createComponent(CustomerNotesComponent);
    fixture.componentRef.setInput('customerId', 'c-1');
    fixture.detectChanges();

    // The load is queued on a microtask so the bound id is in place before it fires.
    await Promise.resolve();
    return fixture;
  }

  function flushNotes(
    fixture: ComponentFixture<CustomerNotesComponent>,
    body: object,
    status?: number,
  ) {
    const request = http.expectOne((r) => r.url === '/api/Customers/c-1/notes');
    if (status) {
      request.flush(body, { status, statusText: 'Error' });
    } else {
      request.flush(body);
    }
    fixture.detectChanges();
    return request;
  }

  it('AC74: notes render newest first with author and time', async () => {
    const fixture = await render();
    flushNotes(fixture, notesPage(NOTES));

    const entries = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('[data-testid="note-list"] li'),
    ).map((li) => li.textContent ?? '');

    expect(entries).toHaveLength(2);
    // The order the server sent, not one this component chose.
    expect(entries[0]).toContain('Called back, awaiting logs.');
    expect(entries[1]).toContain('Customer reported the portal rejects their password.');

    // Each entry names its author and when it was written (AC-74).
    expect(entries[0]).toContain('Dana Support');
    // A time a person can read — the raw instant is a wire detail, not the criterion.
    expect(entries[0]).toContain('26 Aug 2026');
    expect(entries[0]).not.toContain('2026-08-26T11:00:00.000Z');
    expect(entries[1]).toContain('Omar Agent');
  });

  it('AC74: a failed read of the history renders the error state, not an empty history', async () => {
    const fixture = await render();
    flushNotes(fixture, SERVER_FAILURE, 500);

    expect(fixture.componentInstance.state().status).toBe('error');

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[role="alert"]')?.textContent).toContain(
      'The notes could not be loaded',
    );
    expect(el.textContent).not.toContain('No notes recorded for this customer yet');
  });

  it('AC74: a customer with no notes gets the empty state', async () => {
    const fixture = await render();
    flushNotes(fixture, notesPage([]));

    expect(fixture.componentInstance.state().status).toBe('empty');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'No notes recorded for this customer yet',
    );
  });

  /**
   * AC-75's refusal clause. Asserted with `expectNone`, not by checking a disabled button: the
   * criterion is that no request is sent, and a component that posted a blank note and let the
   * server refuse it would satisfy every visual assertion while failing the actual requirement.
   */
  it('AC75: an empty note sends no request', async () => {
    const fixture = await render();
    flushNotes(fixture, notesPage(NOTES));

    fixture.componentInstance.add();

    http.expectNone('/api/Customers/c-1/notes');
  });

  it('AC75: a whitespace-only note sends no request', async () => {
    const fixture = await render();
    flushNotes(fixture, notesPage(NOTES));

    fixture.componentInstance.updateDraft('   \n\t ');
    fixture.detectChanges();

    expect(fixture.componentInstance.canSubmit()).toBe(false);
    fixture.componentInstance.add();

    http.expectNone('/api/Customers/c-1/notes');
  });

  it('AC75: adding a note posts only the body and re-reads the list', async () => {
    const fixture = await render();
    flushNotes(fixture, notesPage([NOTES[1]]));

    fixture.componentInstance.updateDraft('  Called back, awaiting logs.  ');
    fixture.componentInstance.add();

    const post = http.expectOne('/api/Customers/c-1/notes');
    expect(post.request.method).toBe('POST');
    // AC-76's client half — the body is the whole payload; there is no author to send.
    expect(post.request.body).toEqual({ body: 'Called back, awaiting logs.' });
    post.flush({ success: true, code: 'CON035', message: 'OK', data: { id: 'n-2' }, errors: [] });
    fixture.detectChanges();

    // The list is re-read rather than spliced locally, so the note appears without a page reload.
    const reread = http.expectOne((r) => r.url === '/api/Customers/c-1/notes');
    expect(reread.request.method).toBe('GET');
    reread.flush(notesPage(NOTES));
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'Called back, awaiting logs.',
    );
    // The box is cleared, so a second click cannot repeat the note.
    expect(fixture.componentInstance.draft()).toBe('');
  });

  it('AC75: a rejected note shows the server message on the note box', async () => {
    const fixture = await render();
    flushNotes(fixture, notesPage(NOTES));

    fixture.componentInstance.updateDraft('x');
    fixture.componentInstance.add();

    http.expectOne('/api/Customers/c-1/notes').flush(BODY_REQUIRED_ENVELOPE, {
      status: 400,
      statusText: 'Bad Request',
    });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const textarea = el.querySelector('textarea[aria-invalid="true"]');
    expect(textarea).not.toBeNull();
    expect(el.querySelector('#customer-note-error')?.textContent).toContain(
      'A note must have a body',
    );
    // The draft is kept, so the user does not lose what they typed.
    expect(fixture.componentInstance.draft()).toBe('x');
  });

  it('AC75: does not post twice while a note is in flight', async () => {
    const fixture = await render();
    flushNotes(fixture, notesPage(NOTES));

    fixture.componentInstance.updateDraft('Called back.');
    fixture.componentInstance.add();
    fixture.componentInstance.add();

    // expectOne fails if a second post was issued.
    http.expectOne('/api/Customers/c-1/notes').flush({
      success: true,
      code: 'CON035',
      message: 'OK',
      data: { id: 'n-3' },
      errors: [],
    });
    fixture.detectChanges();

    http.expectOne((r) => r.url === '/api/Customers/c-1/notes').flush(notesPage(NOTES));
  });
});
