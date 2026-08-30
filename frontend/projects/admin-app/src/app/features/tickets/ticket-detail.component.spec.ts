import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { envelopeInterceptor, SessionStore } from 'common';
import TicketDetailComponent from './ticket-detail.component';

const ROLE_CLAIM = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

/** A JWT is three base64url segments; SessionStore.roles is computed from the middle one. */
function fakeJwt(roles: string[]): string {
  const encode = (value: unknown) =>
    btoa(JSON.stringify(value)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  return `${encode({ alg: 'none' })}.${encode({ [ROLE_CLAIM]: roles })}.signature`;
}

const TICKET = {
  id: 't-1',
  reference: 'TKT-001001',
  subject: 'Cannot sign in',
  description: 'The portal rejects my password.',
  status: 'Open',
  priority: 'High',
  assigneeId: null,
  createdAt: '2026-08-26T09:00:00Z',
  rowVersion: 'AAAAAAABAdE=',
  customer: { id: 'c-1', name: 'Layla Haddad', email: 'layla@example.com', phone: '+20 100' },
  categoryName: 'Technical',
  history: [
    {
      id: 'h-2',
      changeType: 'StatusChanged',
      fromValue: 'New',
      toValue: 'Open',
      actorId: 'u-1',
      actorName: 'Dana Support',
      occurredAt: '2026-08-26T10:00:00Z',
    },
    {
      id: 'h-1',
      changeType: 'Created',
      fromValue: null,
      toValue: 'New',
      actorId: 'u-1',
      actorName: 'Dana Support',
      occurredAt: '2026-08-26T09:00:00Z',
    },
  ],
  responseDueAt: null,
  resolutionDueAt: null,
  escalationState: 'None',
};

const CONFLICT = {
  success: false,
  code: 'TICKET_MODIFIED_BY_ANOTHER_USER',
  message: 'This ticket was changed by someone else. Reload and try again',
  data: null,
  errors: [],
};

const AGENTS = {
  success: true,
  code: 'CON035',
  message: 'OK',
  data: [{ id: 'a-1', name: 'Omar Agent', email: 'omar@example.com' }],
  errors: [],
};

describe('TicketDetailComponent', () => {
  let http: HttpTestingController;

  function configure(roles: string[]) {
    localStorage.clear();
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([envelopeInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    TestBed.inject(SessionStore).signIn({
      userId: 'u-1',
      email: 'dana@example.com',
      firstName: 'Dana',
      lastName: 'Support',
      accessToken: fakeJwt(roles),
      refreshToken: 'refresh-token',
      accessTokenExpiresAt: '2026-09-01T00:00:00Z',
      refreshTokenExpiresAt: '2026-09-08T00:00:00Z',
      roles,
    });
  }

  async function render(roles: string[]): Promise<ComponentFixture<TicketDetailComponent>> {
    configure(roles);
    const fixture = TestBed.createComponent(TicketDetailComponent);
    fixture.componentRef.setInput('id', 't-1');
    fixture.detectChanges();

    // The load is queued on a microtask so the route input is bound before it fires.
    await Promise.resolve();

    http.expectOne('/api/Tickets/t-1').flush({ success: true, code: 'CON035', message: 'OK', data: TICKET, errors: [] });
    fixture.detectChanges();

    if (roles.includes('Supervisor') || roles.includes('Admin')) {
      http.expectOne('/api/Tickets/assignable-agents').flush(AGENTS);
      fixture.detectChanges();
    }

    return fixture;
  }

  it('AC61: renders the customer summary, the history timeline and the status action', async () => {
    const fixture = await render(['Agent']);
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('[data-testid="customer-summary"]')?.textContent).toContain(
      'Layla Haddad',
    );
    expect(el.querySelector('[data-testid="status-action"]')).not.toBeNull();

    const timeline = el.querySelector('[data-testid="history-timeline"]');
    expect(timeline?.textContent).toContain('Created');
    expect(timeline?.textContent).toContain('StatusChanged');
    // AC-50 — the actor's display name, not a bare id.
    expect(timeline?.textContent).toContain('Dana Support');
    expect(timeline?.textContent).not.toContain('u-1');
  });

  /**
   * AC-61's hidden half. This is a courtesy, not the control: the server answers 403 to an agent
   * regardless of what is rendered, which `AC43_Agent_AssigningAnyTicket_Returns403` proves.
   */
  it('AC61: the assign action is hidden for an agent', async () => {
    const fixture = await render(['Agent']);

    expect(fixture.componentInstance.canAssign()).toBe(false);
    expect(
      (fixture.nativeElement as HTMLElement).querySelector('[data-testid="assign-action"]'),
    ).toBeNull();
  });

  it('AC61: the assign action is offered to a supervisor, populated from the agents endpoint', async () => {
    const fixture = await render(['Supervisor']);

    expect(fixture.componentInstance.canAssign()).toBe(true);
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="assign-action"]')).not.toBeNull();
    expect(el.querySelector('[data-testid="assign-action"]')?.textContent).toContain('Omar Agent');
  });

  /** Offering a move the server would refuse wastes a round trip and reads as a broken control. */
  it('AC61: the status action offers only the transitions permitted from the current status', async () => {
    const fixture = await render(['Agent']);

    // The fixture ticket is Open, so the table permits Assigned and Resolved and nothing else.
    expect(fixture.componentInstance.availableTransitions()).toEqual(['Assigned', 'Resolved']);

    const options = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('#detail-status option'),
    ).map((o) => (o as HTMLOptionElement).value);

    expect(options).toEqual(['', 'Assigned', 'Resolved']);
    expect(options).not.toContain('Closed');
  });

  it('AC61: a status change echoes the rowVersion it read', async () => {
    const fixture = await render(['Agent']);

    fixture.componentInstance.changeStatus('Resolved');

    const request = http.expectOne('/api/Tickets/t-1/status');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ status: 'Resolved', rowVersion: 'AAAAAAABAdE=' });
    request.flush({ success: true, code: 'CON035', message: 'OK', data: { id: 't-1' }, errors: [] });

    // Success re-reads, so the screen never holds a superseded version.
    http.expectOne('/api/Tickets/t-1').flush({ success: true, code: 'CON035', message: 'OK', data: TICKET, errors: [] });
  });

  it('AC61: assigning posts the agent id and the rowVersion', async () => {
    const fixture = await render(['Supervisor']);

    fixture.componentInstance.assign('a-1');

    const request = http.expectOne('/api/Tickets/t-1/assignee');
    expect(request.request.body).toEqual({ assigneeId: 'a-1', rowVersion: 'AAAAAAABAdE=' });
    request.flush({ success: true, code: 'CON035', message: 'OK', data: { id: 't-1' }, errors: [] });

    http.expectOne('/api/Tickets/t-1').flush({ success: true, code: 'CON035', message: 'OK', data: TICKET, errors: [] });
  });

  it('AC506: supervisor can take ownership of an escalated ticket', async () => {
    configure(['Supervisor']);
    const fixture = TestBed.createComponent(TicketDetailComponent);
    fixture.componentRef.setInput('id', 't-1');
    fixture.detectChanges();
    await Promise.resolve();

    http.expectOne('/api/Tickets/t-1').flush({
      success: true,
      code: 'CON035',
      message: 'OK',
      data: { ...TICKET, escalationState: 'Level1', escalationAssigneeId: null, escalationAssigneeName: null },
      errors: [],
    });
    fixture.detectChanges();
    http.expectOne('/api/Tickets/assignable-agents').flush(AGENTS);
    fixture.detectChanges();

    expect(
      (fixture.nativeElement as HTMLElement).querySelector('[data-testid="escalation-owner-action"]'),
    ).not.toBeNull();

    fixture.componentInstance.takeEscalation('a-1');

    const request = http.expectOne('/api/Tickets/t-1/escalation-owner');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ assigneeId: 'a-1', rowVersion: 'AAAAAAABAdE=' });
    request.flush({ success: true, code: 'CON035', message: 'OK', data: { id: 't-1' }, errors: [] });

    http.expectOne('/api/Tickets/t-1').flush({
      success: true,
      code: 'CON035',
      message: 'OK',
      data: { ...TICKET, escalationState: 'Level1', escalationAssigneeId: 'a-1', escalationAssigneeName: 'Omar Agent' },
      errors: [],
    });
  });

  /**
   * On a 409 the local rowVersion is stale by definition. Patching the local copy would leave the
   * screen holding a superseded version and the next attempt would fail identically, so the only
   * honest recovery is a re-read — with the server's explanation still on screen.
   */
  it('AC61: a conflict shows the server message and re-reads the ticket', async () => {
    const fixture = await render(['Agent']);

    fixture.componentInstance.changeStatus('Resolved');

    http.expectOne('/api/Tickets/t-1/status').flush(CONFLICT, {
      status: 409,
      statusText: 'Conflict',
    });
    fixture.detectChanges();

    // The refusal triggers a re-read.
    http.expectOne('/api/Tickets/t-1').flush({ success: true, code: 'CON035', message: 'OK', data: TICKET, errors: [] });
    fixture.detectChanges();

    expect(
      (fixture.nativeElement as HTMLElement).querySelector('[role="alert"]')?.textContent,
    ).toContain('This ticket was changed by someone else');
  });

  it('AC155: renders a countdown for a ticket with a response due date', async () => {
    configure(['Agent']);
    const fixture = TestBed.createComponent(TicketDetailComponent);
    fixture.componentRef.setInput('id', 't-1');
    fixture.detectChanges();
    await Promise.resolve();

    http.expectOne('/api/Tickets/t-1').flush({
      success: true,
      code: 'CON035',
      message: 'OK',
      data: {
        ...TICKET,
        responseDueAt: new Date(Date.now() + 3_600_000).toISOString(),
        resolutionDueAt: null,
        escalationState: 'None',
      },
      errors: [],
    });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Response due');
    expect(el.querySelector('[data-urgency]')).not.toBeNull();
  });

  it('AC58: a failed load renders the error state, not an empty ticket', async () => {
    configure(['Agent']);
    const fixture = TestBed.createComponent(TicketDetailComponent);
    fixture.componentRef.setInput('id', 't-1');
    fixture.detectChanges();
    await Promise.resolve();

    http
      .expectOne('/api/Tickets/t-1')
      .flush(
        {
          success: false,
          code: 'INTERNAL_ERROR',
          message: 'Server exploded',
          data: null,
          errors: [],
        },
        { status: 500, statusText: 'Server Error' },
      );
    fixture.detectChanges();

    expect(fixture.componentInstance.state().status).toBe('error');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Server exploded');
  });

  it('US202_TicketDetail_RendersMessageTimelineForLoadedTicket', async () => {
    configure(['Agent']);
    const fixture = TestBed.createComponent(TicketDetailComponent);
    fixture.componentRef.setInput('id', 't-1');
    fixture.detectChanges();
    await Promise.resolve();

    http.expectOne('/api/Tickets/t-1').flush({ success: true, code: 'CON035', message: 'OK', data: TICKET, errors: [] });
    fixture.detectChanges();

    // The message timeline child is only created once the ticket is loaded; it fires its own read.
    await Promise.resolve();
    http
      .expectOne('/api/Tickets/t-1/messages')
      .flush({
        success: true,
        code: 'CON035',
        message: 'OK',
        data: [
          {
            id: 'm-1',
            direction: 'Inbound',
            channel: 'Email',
            subject: null,
            body: 'The portal rejects my password.',
            senderId: 'c-1',
            senderName: 'Layla Haddad',
            sentAt: '2026-08-26T09:05:00Z',
          },
        ],
        errors: [],
      });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const timeline = el.querySelector('[data-testid="message-list"]');
    expect(timeline).not.toBeNull();
    expect(timeline?.textContent).toContain('Layla Haddad');
    expect(timeline?.textContent).toContain('The portal rejects my password.');
    expect(timeline?.textContent).toContain('Email');
  });

  it('AC409_DetailRendersTimelineMetadataAndAiRegions: renders timeline, metadata rail, and AI panel', async () => {
    const fixture = await render(['Agent']);
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="history-timeline"]')).not.toBeNull();
    expect(el.querySelector('[data-testid="customer-summary"]')).not.toBeNull();
    expect(el.querySelector('admin-ai-panel')).not.toBeNull();
  });

  it('AC416_TicketScreensDistinguishLoadingEmptyAndError: loading state is displayed while fetching ticket', () => {
    configure(['Agent']);
    const fixture = TestBed.createComponent(TicketDetailComponent);
    fixture.componentRef.setInput('id', 't-1');
    fixture.detectChanges();
    expect(fixture.componentInstance.state().status).toBe('loading');
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[role="status"]')).not.toBeNull();
  });

  it('AC417_MissingTicketCapabilitiesRenderUnavailableWithoutControls: unassigned ticket uses placeholder', async () => {
    const fixture = await render(['Agent']);
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Unassigned');
  });

  it('AC418_TicketFormsAndActionsAreKeyboardAccessible: action selects have accessible labels', async () => {
    const fixture = await render(['Supervisor']);
    const el = fixture.nativeElement as HTMLElement;
    const selects = el.querySelectorAll('select');
    expect(selects.length).toBeGreaterThanOrEqual(1);
    for (const sel of Array.from(selects)) {
      expect(sel.getAttribute('id')).toBeTruthy();
    }
  });
});
