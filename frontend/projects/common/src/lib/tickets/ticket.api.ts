import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { PagedResult } from '../api/api-response';

/** The four values `TicketPriority` allows. Kept in step with the backend value object. */
export const TICKET_PRIORITIES = ['Low', 'Normal', 'High', 'Urgent'] as const;
export type TicketPriority = (typeof TICKET_PRIORITIES)[number];

/** US-923. The matrix inputs — priority is derived from these, never set directly. */
export const TICKET_IMPACTS = ['Low', 'Medium', 'High'] as const;
export type TicketImpact = (typeof TICKET_IMPACTS)[number];
export const TICKET_URGENCIES = ['Low', 'Medium', 'High'] as const;
export type TicketUrgency = (typeof TICKET_URGENCIES)[number];

/**
 * A client-side **preview** of the server's matrix (spec decision 2026-08-31: matrix-only
 * priority). Display only — the create/reclassify response carries the authoritative value, and
 * this must never be sent back to the server as if it were an input.
 */
const PRIORITY_MATRIX: Readonly<Record<TicketImpact, Readonly<Record<TicketUrgency, TicketPriority>>>> = {
  Low: { Low: 'Low', Medium: 'Low', High: 'Normal' },
  Medium: { Low: 'Low', Medium: 'Normal', High: 'High' },
  High: { Low: 'Normal', Medium: 'High', High: 'Urgent' },
};

export function derivePriority(impact: TicketImpact, urgency: TicketUrgency): TicketPriority {
  return PRIORITY_MATRIX[impact][urgency];
}

/** The eight lifecycle states. The transitions between them are a server concern (AC-501). */
export const TICKET_STATUSES = ['New', 'Open', 'Assigned', 'In Progress', 'Waiting for Customer', 'Waiting for Internal Team', 'Resolved', 'Closed'] as const;
export type TicketStatus = (typeof TICKET_STATUSES)[number];

/** Matches the backend's `TicketListItemDto`. No description — a list row does not need one. */
export interface TicketListItem {
  readonly id: string;
  readonly reference: string;
  readonly subject: string;
  readonly status: TicketStatus;
  readonly priority: TicketPriority;
  readonly customerId: string;
  readonly customerName: string;
  readonly categoryId: string;
  readonly categoryName: string;
  readonly assigneeId: string | null;
  readonly assigneeName?: string | null;
  readonly createdAt: string;
  /** Optional until the list endpoint includes channel provenance for every ticket. */
  readonly channel?: MessageChannel | null;
  /** Optional until SLA summary fields are projected into the list endpoint. */
  readonly responseDueAt?: string | null;
  readonly resolutionDueAt?: string | null;
  /** FEAT-17 second slice addendum, AC-158. None/Warning/Level1/Level2/Level3 (BR-32). */
  readonly escalationState: string;
  /** US-924 / AC-924.4. Normalized values, alphabetical; empty when untagged. */
  readonly tags: readonly string[];
}

export interface TicketFilters {
  readonly page?: number;
  readonly pageSize?: number;
  readonly status?: TicketStatus | null;
  readonly mine?: boolean;
  /** Tickets with no assignee. `AC-82`, and a backend filter flag rather than a magic guid. */
  readonly unassigned?: boolean;
  /** US-924 / AC-924.4. Only tickets carrying this tag. */
  readonly tag?: string | null;
}

export interface CreateTicketRequest {
  readonly subject: string;
  readonly description: string;
  readonly customerId: string;
  readonly categoryId: string;
  readonly impact: TicketImpact;
  readonly urgency: TicketUrgency;
}

/**
 * One ticket attachment as the list/download routes return it (TA-4/TA-10).
 * <c>id</c> is the link id, which is what the content route addresses.
 */
export interface TicketAttachment {
  readonly id: string;
  readonly originalFileName: string;
  readonly contentType: string;
  readonly sizeBytes: number;
  readonly uploadedByName: string;
  readonly createdAt: string;
}

/** Mirrors the server's 10 MB cap (TA-2) — the server still enforces it independently. */
export const MAX_TICKET_ATTACHMENT_BYTES = 10 * 1024 * 1024;

/** The same allowlist as customer attachments — kept in the server's order (TA-2). */
export const ALLOWED_TICKET_ATTACHMENT_TYPES = [
  'image/png',
  'image/jpeg',
  'image/gif',
  'application/pdf',
  'text/plain',
] as const;

export interface CustomerOption {
  readonly id: string;
  readonly name: string;
  readonly email: string;
}

export interface CategoryOption {
  readonly id: string;
  readonly name: string;
}

export interface AssignableAgent {
  readonly id: string;
  readonly name: string;
  readonly email: string;
}

export interface CustomerSummary {
  readonly id: string;
  readonly name: string;
  readonly email: string;
  readonly phone: string | null;
}

/** The two values `Direction` allows — FEAT-14, AC-101. */
export const MESSAGE_DIRECTIONS = ['Inbound', 'Outbound'] as const;
export type MessageDirection = (typeof MESSAGE_DIRECTIONS)[number];

/** The six channels supported for ticket messages — FEAT-24..27, CC-24. */
export const MESSAGE_CHANNELS = ['System', 'Email', 'WhatsApp', 'SMS', 'WebForm', 'LiveChat'] as const;
export type MessageChannel = (typeof MESSAGE_CHANNELS)[number];

/** Matches the backend's `TicketMessageDto`. Oldest first, as the server orders them. */
export interface TicketMessage {
  readonly id: string;
  readonly direction: MessageDirection;
  readonly channel: MessageChannel;
  readonly subject: string | null;
  readonly body: string;
  readonly senderId: string;
  readonly senderName: string;
  readonly sentAt: string;
}

/** AC-101 — no author field: the sender comes from the session, the same rule CustomerNote follows. */
export interface RecordTicketMessageRequest {
  readonly direction: MessageDirection;
  readonly channel: MessageChannel;
  readonly subject?: string;
  readonly body: string;
}

export interface TicketHistoryEntry {
  readonly id: string;
  readonly changeType: 'Created' | 'Assigned' | 'Reassigned' | 'StatusChanged' | 'Reopened';
  readonly fromValue: string | null;
  readonly toValue: string | null;
  readonly actorId: string;
  readonly actorName: string;
  readonly occurredAt: string;
}

/**
 * One edge of the link graph as seen from this ticket (US-925, AC-925.5). `direction` is
 * `'Outbound'` when this ticket is the source ("duplicate of ..."), `'Inbound'` when it is the
 * target ("duplicated by ...").
 */
export interface TicketLink {
  readonly id: string;
  readonly linkType: 'RelatedTo' | 'DuplicateOf';
  readonly direction: 'Outbound' | 'Inbound';
  readonly otherTicketId: string;
  readonly otherReference: string;
  readonly otherSubject: string;
}

/** Matches the backend's `TicketDetailDto`. */
export interface TicketDetail {
  readonly id: string;
  readonly reference: string;
  readonly subject: string;
  readonly description: string;
  readonly status: TicketStatus;
  readonly priority: TicketPriority;
  readonly assigneeId: string | null;
  readonly assigneeName: string | null;
  readonly createdAt: string;
  /** The concurrency token, opaque. Echoed back on every mutation (AC-41). */
  readonly rowVersion: string;
  readonly customer: CustomerSummary;
  readonly categoryName: string;
  readonly history: readonly TicketHistoryEntry[];
  /** FEAT-17, AC-128/AC-129. Null when no active SLAPolicy matched at creation. */
  readonly responseDueAt: string | null;
  readonly resolutionDueAt: string | null;
  /** FEAT-17 second slice, AC-137/AC-138. None/Warning/Level1/Level2/Level3 (BR-32). */
  readonly escalationState: string;
  readonly firstResponseAt: string | null;
  readonly lastResponseAt: string | null;
  readonly resolvedAt: string | null;
  readonly closedAt: string | null;
  readonly escalationAssigneeId: string | null;
  readonly escalationAssigneeName: string | null;
  /** US-922 / AC-922.6. Null / 0 until the ticket has been resolved / reopened. */
  readonly resolutionCode: string | null;
  readonly resolutionNotes: string | null;
  readonly reopenCount: number;
  /** US-923 / AC-923.6. Null on tickets created before FEAT-32 (spec A1). */
  readonly impact: string | null;
  readonly urgency: string | null;
  /** US-924. Normalized values, alphabetical. */
  readonly tags: readonly string[];
  /** US-925. */
  readonly links: readonly TicketLink[];
}

/** US-922. Kept in step with the backend's `TicketResolutionCode` value object. */
export const RESOLUTION_CODES = ['Fixed', 'Workaround', 'Duplicate', 'CannotReproduce', 'NoResponse'] as const;
export type ResolutionCode = (typeof RESOLUTION_CODES)[number];

/**
 * The transitions the server permits from each status.
 *
 * A **copy** of the table in the backend's `TicketStatus` value object, and the server stays the
 * authority — this exists so the action does not offer a move it knows will be refused. Two copies
 * can drift, and the mitigation is that a drifted client is a worse experience rather than a hole:
 * an offered-but-forbidden transition still comes back 409, which the detail screen renders.
 */
export const PERMITTED_TRANSITIONS: Readonly<Record<TicketStatus, readonly TicketStatus[]>> = {
  New: ['Open'],
  Open: ['Assigned', 'Resolved'],
  Assigned: ['In Progress'],
  'In Progress': ['Waiting for Customer', 'Waiting for Internal Team', 'Resolved'],
  'Waiting for Customer': ['In Progress'],
  'Waiting for Internal Team': ['In Progress'],
  Resolved: ['In Progress', 'Closed'],
  Closed: ['In Progress'],
};

/**
 * Ticket calls. Catches nothing: failures arrive as `ApiError` from the envelope interceptor, and a
 * service that swallowed them would be the first step towards rendering a server fault as "no
 * tickets" (AC-58).
 */
@Injectable({ providedIn: 'root' })
export class TicketApi {
  private readonly http = inject(HttpClient);

  list(filters: TicketFilters = {}): Observable<PagedResult<TicketListItem>> {
    let params = new HttpParams()
      .set('page', String(filters.page ?? 1))
      .set('pageSize', String(filters.pageSize ?? 10));

    // Absent rather than empty: `status=` would reach the server as a blank string, and the
    // backend refuses an unrecognised status value with a 400 (AC-33).
    if (filters.status) {
      params = params.set('status', filters.status);
    }

    if (filters.mine) {
      params = params.set('mine', 'true');
    }

    // Sent only when true, for the same reason `status` is omitted when unset: the server reads
    // the filter value rather than ignoring a blank one, so `unassigned=` is not `unassigned`
    // absent. `false` means "do not filter", which is expressed by not sending the parameter.
    if (filters.unassigned) {
      params = params.set('unassigned', 'true');
    }

    if (filters.tag) {
      params = params.set('tag', filters.tag);
    }

    return this.http.get<PagedResult<TicketListItem>>('/api/Tickets', { params });
  }

  /**
   * How many tickets match, without pulling the rows.
   *
   * `pageSize=1` because only `totalCount` is read — the single returned row is discarded. The
   * dashboard's tiles (`AC-78`, `AC-82`) are four small round trips against a purpose-made
   * aggregate endpoint, which is a deliberate trade for one agent's queue rather than an
   * oversight. If it ever measures badly, that is a finding to record, not a guess to build on.
   */
  countOnly(filters: TicketFilters = {}): Observable<number> {
    return this.list({ ...filters, page: 1, pageSize: 1 }).pipe(map((page) => page.totalCount));
  }

  create(request: CreateTicketRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>('/api/Tickets', request);
  }

  /** The create form's customer picker. Paged server-side; the form asks for one page. */
  searchCustomers(search: string): Observable<{ items: readonly CustomerOption[] }> {
    const params = new HttpParams().set('pageSize', '20').set('search', search);
    return this.http.get<{ items: readonly CustomerOption[] }>('/api/Customers', { params });
  }

  /** The category picker. A closed, seeded list of four, so it is unpaged. */
  listCategories(): Observable<readonly CategoryOption[]> {
    return this.http.get<readonly CategoryOption[]>('/api/Categories');
  }

  get(id: string): Observable<TicketDetail> {
    return this.http.get<TicketDetail>(`/api/Tickets/${id}`);
  }

  /** Lists a ticket's attachments (TA-10). */
  listAttachments(id: string): Observable<readonly TicketAttachment[]> {
    return this.http.get<readonly TicketAttachment[]>(`/api/Tickets/${id}/attachments`);
  }

  /**
   * One file against a ticket (TA-9). **No `Content-Type` header** — multipart carries a boundary
   * the browser generates, and setting it by hand breaks parsing (same load-bearing absence as the
   * customer attachment upload).
   */
  uploadAttachment(id: string, file: File): Observable<{ id: string }> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<{ id: string }>(`/api/Tickets/${id}/attachments`, form);
  }

  /** The content route for an attachment, named so a component can fetch it with the auth header. */
  attachmentContentUrl(id: string, attachmentId: string): string {
    return `/api/Tickets/${id}/attachments/${attachmentId}/content`;
  }

  /** The attachment bytes as a blob. `HttpClient` puts the auth header on; a bare URL cannot. */
  downloadAttachment(id: string, attachmentId: string): Observable<Blob> {
    return this.http.get(this.attachmentContentUrl(id, attachmentId), {
      responseType: 'blob',
    });
  }

  /**
   * Moves the ticket along its lifecycle. `rowVersion` is the value read from `get` — the server
   * compares it to detect a lost update (AC-41), so it must be echoed, not invented.
   *
   * `resolutionCode`/`resolutionNotes` are required by the server only when `status` is
   * `'Resolved'` (AC-922.1); every other transition ignores them if present, so callers simply
   * omit them (US-922).
   */
  changeStatus(
    id: string,
    status: TicketStatus,
    rowVersion: string,
    resolutionCode?: string,
    resolutionNotes?: string,
  ): Observable<unknown> {
    return this.http.post(`/api/Tickets/${id}/status`, {
      status,
      rowVersion,
      ...(resolutionCode !== undefined ? { resolutionCode, resolutionNotes } : {}),
    });
  }

  assign(id: string, assigneeId: string, rowVersion: string): Observable<unknown> {
    return this.http.post(`/api/Tickets/${id}/assignee`, { assigneeId, rowVersion });
  }

  /**
   * US-923 / AC-923.2. Sets the matrix inputs; the server re-derives priority. There is no
   * endpoint that sets priority directly — this is the only mutation path it has post-creation.
   */
  reclassify(id: string, impact: TicketImpact, urgency: TicketUrgency, rowVersion: string): Observable<unknown> {
    return this.http.post(`/api/Tickets/${id}/classification`, { impact, urgency, rowVersion });
  }

  takeEscalation(id: string, assigneeId: string, rowVersion: string): Observable<unknown> {
    return this.http.post(`/api/Tickets/${id}/escalation-owner`, { assigneeId, rowVersion });
  }

  /** Supervisors only — an agent gets 403 from the server (AC-43). */
  listAssignableAgents(): Observable<readonly AssignableAgent[]> {
    return this.http.get<readonly AssignableAgent[]>('/api/Tickets/assignable-agents');
  }

  /** A ticket's message timeline, oldest first (AC-106). Unpaginated — see the spec's A6. */
  listMessages(id: string): Observable<readonly TicketMessage[]> {
    return this.http.get<readonly TicketMessage[]>(`/api/Tickets/${id}/messages`);
  }

  /** AC-101 — logs a message against a ticket. The sender is never sent; it comes from the token. */
  recordMessage(id: string, request: RecordTicketMessageRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`/api/Tickets/${id}/messages`, request);
  }

  /** US-924 / AC-924.1. The server normalizes and validates (charset, length, duplicate, limit). */
  addTag(id: string, value: string): Observable<unknown> {
    return this.http.post(`/api/Tickets/${id}/tags`, { value });
  }

  /** US-924. `value` should be the already-normalized tag as rendered — the route carries it raw. */
  removeTag(id: string, value: string): Observable<unknown> {
    return this.http.delete(`/api/Tickets/${id}/tags/${encodeURIComponent(value)}`);
  }

  /** US-925 / AC-925.1. `targetReference` is the other ticket's TKT-nnnnnn reference. */
  addLink(id: string, linkType: 'RelatedTo' | 'DuplicateOf', targetReference: string): Observable<unknown> {
    return this.http.post(`/api/Tickets/${id}/links`, { linkType, targetReference });
  }

  /** US-925 / AC-925.4. */
  removeLink(id: string, linkId: string): Observable<unknown> {
    return this.http.delete(`/api/Tickets/${id}/links/${linkId}`);
  }
}
