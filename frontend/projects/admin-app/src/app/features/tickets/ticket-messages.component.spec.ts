import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { TicketApi, TicketMessage } from 'common';
import { TicketMessagesComponent } from './ticket-messages.component';

const MESSAGES: readonly TicketMessage[] = [
  {
    id: 'm1',
    direction: 'Inbound',
    channel: 'System',
    subject: 'First note',
    body: 'Oldest body',
    senderId: 'u1',
    senderName: 'Alice',
    sentAt: '2026-08-20T08:00:00Z',
  },
  {
    id: 'm2',
    direction: 'Outbound',
    channel: 'Email',
    subject: null,
    body: 'Middle body',
    senderId: 'u2',
    senderName: 'Bob',
    sentAt: '2026-08-21T09:30:00Z',
  },
  {
    id: 'm3',
    direction: 'Inbound',
    channel: 'System',
    subject: null,
    body: 'Newest body',
    senderId: 'u3',
    senderName: 'Carol',
    sentAt: '2026-08-22T11:45:00Z',
  },
];

describe('TicketMessagesComponent', () => {
  let listMessages: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    listMessages = vi.fn();
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [TicketMessagesComponent],
      providers: [
        {
          provide: TicketApi,
          useValue: { listMessages, recordMessage: vi.fn() },
        },
      ],
    });
  });

  async function create() {
    const fixture = TestBed.createComponent(TicketMessagesComponent);
    fixture.componentRef.setInput('ticketId', 't1');
    fixture.detectChanges();
    // The component fires load() from a queueMicrotask, so flush it and the resulting state
    // settle before any assertion.
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  it('US202_MessageTimeline_RendersOldestFirstWithDirectionChannelSenderBodyAndTime', async () => {
    listMessages.mockReturnValue(of([...MESSAGES]));
    const fixture = await create();
    const el = fixture.nativeElement as HTMLElement;

    expect(listMessages).toHaveBeenCalledWith('t1');

    const list = el.querySelector('[data-testid="message-list"]');
    expect(list).not.toBeNull();

    const rows = Array.from(list!.querySelectorAll('li'));
    expect(rows.length).toBe(3);

    // Oldest first, exactly as served, no client re-sort.
    const bodies = rows.map((row) => row.textContent);
    expect(bodies[0]).toContain('Oldest body');
    expect(bodies[1]).toContain('Middle body');
    expect(bodies[2]).toContain('Newest body');

    // Sender provenance, channel, and body per row.
    expect(bodies[0]).toContain('Alice');
    expect(bodies[1]).toContain('Bob');
    expect(bodies[2]).toContain('Carol');
    expect(bodies[1]).toContain('Email');
    expect(bodies[0]).toContain('In-app');

    // Time rendered both as a datetime attribute and as text.
    const times = Array.from(list!.querySelectorAll('time[datetime]'));
    expect((times[0] as HTMLElement).getAttribute('datetime')).toBe('2026-08-20T08:00:00Z');
    expect((times[1] as HTMLElement).getAttribute('datetime')).toBe('2026-08-21T09:30:00Z');
    expect((times[2] as HTMLElement).getAttribute('datetime')).toBe('2026-08-22T11:45:00Z');
  });

  it('US202_MessageTimeline_RendersDistinctEmptyState', async () => {
    listMessages.mockReturnValue(of([]));
    const fixture = await create();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('[data-testid="message-list"]')).toBeNull();
    expect(el.textContent).toContain('No messages');
  });

  it('US202_MessageTimeline_RendersLoadFailureInsteadOfEmptyState', async () => {
    listMessages.mockReturnValue(throwError(() => new Error('network down')));
    const fixture = await create();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.textContent).toContain('Connection interrupted');
    expect(el.textContent).toContain('Try again');
    expect(el.querySelector('[data-testid="message-list"]')).toBeNull();
    expect(el.textContent).not.toContain('No messages');
  });

  it('US202_MessageTimeline_UsesTicketApiListMessages', async () => {
    listMessages.mockReturnValue(of([]));
    const fixture = await create();
    fixture.detectChanges();
    expect(listMessages).toHaveBeenCalledTimes(1);
    expect(listMessages).toHaveBeenCalledWith('t1');
  });
});
