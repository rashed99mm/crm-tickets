import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { vi } from 'vitest';
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
  resolutionCode: null,
  resolutionNotes: null,
  reopenCount: 0,
  impact: null,
  urgency: null,
  tags: [],
  links: [],
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

    fixture.componentInstance.setTab('history');
    fixture.detectChanges();
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

  it('AC61: a non-resolving status change echoes the rowVersion it read', async () => {
    const fixture = await render(['Agent']);

    fixture.componentInstance.selectStatus('Assigned');

    const request = http.expectOne('/api/Tickets/t-1/status');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ status: 'Assigned', rowVersion: 'AAAAAAABAdE=' });
    request.flush({ success: true, code: 'CON035', message: 'OK', data: { id: 't-1' }, errors: [] });

    // Success re-reads, so the screen never holds a superseded version.
    http.expectOne('/api/Tickets/t-1').flush({ success: true, code: 'CON035', message: 'OK', data: TICKET, errors: [] });
  });

  it('AC922_7: selecting Resolved opens the inline resolve form instead of committing bare', async () => {
    const fixture = await render(['Agent']);

    fixture.componentInstance.selectStatus('Resolved');
    fixture.detectChanges();

    // No request yet — the form is showing, not submitting.
    http.expectNone('/api/Tickets/t-1/status');
    expect(fixture.componentInstance.showResolveForm()).toBe(true);
    expect(
      (fixture.nativeElement as HTMLElement).querySelector('[data-testid="resolve-form"]'),
    ).not.toBeNull();
  });

  it('AC922_7: submitting the resolve form sends code, notes and rowVersion', async () => {
    const fixture = await render(['Agent']);

    fixture.componentInstance.selectStatus('Resolved');
    fixture.detectChanges();
    fixture.componentInstance.submitResolve('Fixed', 'Reset the password and confirmed sign-in.');

    const request = http.expectOne('/api/Tickets/t-1/status');
    expect(request.request.body).toEqual({
      status: 'Resolved',
      rowVersion: 'AAAAAAABAdE=',
      resolutionCode: 'Fixed',
      resolutionNotes: 'Reset the password and confirmed sign-in.',
    });
    request.flush({ success: true, code: 'CON035', message: 'OK', data: { id: 't-1' }, errors: [] });

    http.expectOne('/api/Tickets/t-1').flush({
      success: true,
      code: 'CON035',
      message: 'OK',
      data: { ...TICKET, status: 'Resolved', resolutionCode: 'Fixed', resolutionNotes: 'Reset the password and confirmed sign-in.', reopenCount: 0 },
      errors: [],
    });
    fixture.detectChanges();

    expect(fixture.componentInstance.showResolveForm()).toBe(false);
    expect(
      (fixture.nativeElement as HTMLElement).querySelector('[data-testid="resolution-banner"]')?.textContent,
    ).toContain('Fixed');
  });

  it('AC922_7: a resolved ticket shows its resolution and reopen count', async () => {
    configure(['Agent']);
    const fixture = TestBed.createComponent(TicketDetailComponent);
    fixture.componentRef.setInput('id', 't-1');
    fixture.detectChanges();
    await Promise.resolve();

    http.expectOne('/api/Tickets/t-1').flush({
      success: true,
      code: 'CON035',
      message: 'OK',
      data: { ...TICKET, status: 'Resolved', resolutionCode: 'Workaround', resolutionNotes: 'Cleared the cache.', reopenCount: 2 },
      errors: [],
    });
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const banner = el.querySelector('[data-testid="resolution-banner"]');
    expect(banner?.textContent).toContain('Workaround');
    expect(banner?.textContent).toContain('Cleared the cache.');
    expect(el.querySelector('[data-testid="reopen-count"]')?.textContent).toContain('2');
  });

  it('AC61: assigning posts the agent id and the rowVersion', async () => {
    const fixture = await render(['Supervisor']);

    fixture.componentInstance.assign('a-1');

    const request = http.expectOne('/api/Tickets/t-1/assignee');
    expect(request.request.body).toEqual({ assigneeId: 'a-1', rowVersion: 'AAAAAAABAdE=' });
    request.flush({ success: true, code: 'CON035', message: 'OK', data: { id: 't-1' }, errors: [] });

    http.expectOne('/api/Tickets/t-1').flush({ success: true, code: 'CON035', message: 'OK', data: TICKET, errors: [] });
  });

  it('AC923_7: reclassify posts impact, urgency and rowVersion', async () => {
    const fixture = await render(['Agent']);

    fixture.componentInstance.reclassify('High', 'High');

    const request = http.expectOne('/api/Tickets/t-1/classification');
    expect(request.request.body).toEqual({ impact: 'High', urgency: 'High', rowVersion: 'AAAAAAABAdE=' });
    request.flush({ success: true, code: 'CON035', message: 'OK', data: { id: 't-1' }, errors: [] });

    http.expectOne('/api/Tickets/t-1').flush({ success: true, code: 'CON035', message: 'OK', data: { ...TICKET, impact: 'High', urgency: 'High' }, errors: [] });
  });

  it('AC924_5: adding a tag posts the value and re-reads the ticket', async () => {
    const fixture = await render(['Agent']);

    fixture.componentInstance.newTagValue.set('billing');
    fixture.componentInstance.addTag('billing');

    const request = http.expectOne('/api/Tickets/t-1/tags');
    expect(request.request.body).toEqual({ value: 'billing' });
    request.flush({ success: true, code: 'CON035', message: 'OK', data: {}, errors: [] });

    http.expectOne('/api/Tickets/t-1').flush({
      success: true, code: 'CON035', message: 'OK', data: { ...TICKET, tags: ['billing'] }, errors: [],
    });
    fixture.detectChanges();

    expect(fixture.componentInstance.newTagValue()).toBe('');
    fixture.componentInstance.setTab('info');
    fixture.detectChanges();
    const chips = (fixture.nativeElement as HTMLElement).querySelectorAll('[data-testid="tag-chip"]');
    expect(chips.length).toBe(1);
    expect(chips[0].textContent).toContain('billing');
  });

  it('AC924_5: removing a tag deletes it', async () => {
    configure(['Agent']);
    const fixture = TestBed.createComponent(TicketDetailComponent);
    fixture.componentRef.setInput('id', 't-1');
    fixture.detectChanges();
    await Promise.resolve();
    http.expectOne('/api/Tickets/t-1').flush({
      success: true, code: 'CON035', message: 'OK', data: { ...TICKET, tags: ['billing'] }, errors: [],
    });
    fixture.detectChanges();

    fixture.componentInstance.removeTag('billing');

    const request = http.expectOne('/api/Tickets/t-1/tags/billing');
    expect(request.request.method).toBe('DELETE');
    request.flush({ success: true, code: 'CON035', message: 'OK', data: {}, errors: [] });

    http.expectOne('/api/Tickets/t-1').flush({
      success: true, code: 'CON035', message: 'OK', data: { ...TICKET, tags: [] }, errors: [],
    });
  });

  it('AC925_5: adding a link posts type and target reference', async () => {
    const fixture = await render(['Agent']);

    fixture.componentInstance.addLink('RelatedTo', 'TKT-002000');

    const request = http.expectOne('/api/Tickets/t-1/links');
    expect(request.request.body).toEqual({ linkType: 'RelatedTo', targetReference: 'TKT-002000' });
    request.flush({ success: true, code: 'CON035', message: 'OK', data: {}, errors: [] });

    http.expectOne('/api/Tickets/t-1').flush({
      success: true, code: 'CON035', message: 'OK',
      data: { ...TICKET, links: [{ id: 'l-1', linkType: 'RelatedTo', direction: 'Outbound', otherTicketId: 't-2', otherReference: 'TKT-002000', otherSubject: 'Billing question' }] },
      errors: [],
    });
    fixture.detectChanges();

    fixture.componentInstance.setTab('info');
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="link-row"]')?.textContent).toContain('TKT-002000');
  });

  it('AC925_5: a DuplicateOf link renders directionally', async () => {
    configure(['Agent']);
    const fixture = TestBed.createComponent(TicketDetailComponent);
    fixture.componentRef.setInput('id', 't-1');
    fixture.detectChanges();
    await Promise.resolve();
    http.expectOne('/api/Tickets/t-1').flush({
      success: true, code: 'CON035', message: 'OK',
      data: { ...TICKET, links: [{ id: 'l-1', linkType: 'DuplicateOf', direction: 'Inbound', otherTicketId: 't-2', otherReference: 'TKT-002000', otherSubject: 'Same issue' }] },
      errors: [],
    });
    fixture.detectChanges();

    fixture.componentInstance.setTab('info');
    fixture.detectChanges();
    const row = (fixture.nativeElement as HTMLElement).querySelector('[data-testid="link-row"]');
    expect(row?.textContent).toContain('TKT-002000');
    // Inbound DuplicateOf reads "duplicated by", not "duplicate of".
    expect(row?.textContent?.toLowerCase()).toContain('duplicated by');
  });

  it('AC925_5: removing a link deletes by id', async () => {
    configure(['Agent']);
    const fixture = TestBed.createComponent(TicketDetailComponent);
    fixture.componentRef.setInput('id', 't-1');
    fixture.detectChanges();
    await Promise.resolve();
    http.expectOne('/api/Tickets/t-1').flush({
      success: true, code: 'CON035', message: 'OK',
      data: { ...TICKET, links: [{ id: 'l-1', linkType: 'RelatedTo', direction: 'Outbound', otherTicketId: 't-2', otherReference: 'TKT-002000', otherSubject: 'Billing question' }] },
      errors: [],
    });
    fixture.detectChanges();

    fixture.componentInstance.removeLink('l-1');

    const request = http.expectOne('/api/Tickets/t-1/links/l-1');
    expect(request.request.method).toBe('DELETE');
    request.flush({ success: true, code: 'CON035', message: 'OK', data: {}, errors: [] });

    http.expectOne('/api/Tickets/t-1').flush({
      success: true, code: 'CON035', message: 'OK', data: { ...TICKET, links: [] }, errors: [],
    });
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

    fixture.componentInstance.selectStatus('Assigned');

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

    fixture.componentInstance.setTab('info');
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

    fixture.componentInstance.setTab('messages');
    fixture.detectChanges();

    // The message timeline child is only created once the Messages tab is active; it fires its own read.
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
    fixture.componentInstance.setTab('history');
    fixture.detectChanges();
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

  /**
   * The header's reference chip is a copy button, not decoration — an agent pastes the reference
   * into chats and call notes constantly. The chip only flips to "Copied" once the clipboard write
   * actually resolves, so a refused permission cannot be reported as a successful copy.
   */
  it('AC61: the header reference chip copies the reference to the clipboard', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    vi.stubGlobal('navigator', { ...navigator, clipboard: { writeText } });

    const fixture = await render(['Agent']);
    const chip = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>(
      'button[title="Copy reference"]',
    );
    expect(chip).not.toBeNull();
    expect(chip?.textContent).toContain('TKT-001001');

    chip?.click();
    await Promise.resolve();

    expect(writeText).toHaveBeenCalledWith('TKT-001001');
    expect(fixture.componentInstance.referenceCopied()).toBe(true);

    vi.unstubAllGlobals();
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
