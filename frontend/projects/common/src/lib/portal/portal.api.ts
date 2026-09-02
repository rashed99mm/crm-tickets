import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { CategoryOption, TicketPriority } from '../tickets/ticket.api';
import { StaffProfile, UpdateProfileRequest } from '../auth/staff.api';

/** The message timeline shown on a portal ticket (US-413, PJ-15). */
export interface PortalMessage {
  readonly direction: string;
  readonly body: string;
  readonly sentAt: string;
}

/** A ticket row as the customer sees it in their own list (US-405, PJ-8). */
export interface PortalTicketListItem {
  readonly id: string;
  readonly reference: string;
  readonly subject: string;
  readonly status: string;
  readonly createdAt: string;
}

/** A ticket's full detail for the customer (US-406, PJ-9/15/16). */
export interface PortalTicketDetail {
  readonly id: string;
  readonly reference: string;
  readonly subject: string;
  readonly description: string;
  readonly status: string;
  readonly priority: TicketPriority;
  readonly createdAt: string;
  readonly messages: readonly PortalMessage[];
  readonly surveySubmitted: boolean;
}

/**
 * The portal create payload — no customerId; that comes from the signed-in session (PJ-8).
 *
 * And no priority. `PortalCreateTicketRequest` on the server (`PortalController.cs:273`) is
 * `(Subject, Description, CategoryId)`, so a `priority` sent here was dropped as an unknown
 * property and never reached a ticket. That is deliberate — US-923 / spec A2 has customer-origin
 * tickets not self-classifying, with the server deriving priority from impact and urgency — so the
 * field was removed rather than wired up. `PortalTicketDetail.priority` is unaffected: reading the
 * priority the server assigned is a different question from letting the customer set it.
 */
export interface CreatePortalTicketRequest {
  readonly subject: string;
  readonly description: string;
  readonly categoryId: string;
}

/** One ticket attachment as the portal list/download routes return it (TA-4/TA-7). */
export interface PortalTicketAttachment {
  readonly id: string;
  readonly originalFileName: string;
  readonly contentType: string;
  readonly sizeBytes: number;
  readonly uploadedByName: string;
  readonly createdAt: string;
}

/** The portal survey payload (US-408/US-409, PJ-11/12). */
export interface SubmitSurveyRequest {
  readonly rating: number;
  readonly comment?: string;
}

export type PortalProfile = StaffProfile;
export type UpdatePortalProfileRequest = UpdateProfileRequest;

/**
 * Customer portal calls. Every route is under `/api/portal` except the category picker, which reuses
 * the public `/api/Categories`. The customer id is never sent in a body — it is derived from the
 * token on the server (PJ-3/4), so the submit payload deliberately carries no customerId.
 */
@Injectable({ providedIn: 'root' })
export class PortalApi {
  private readonly http = inject(HttpClient);

  getProfile(): Observable<PortalProfile> {
    return this.http.get<PortalProfile>('/api/portal/profile');
  }

  updateProfile(request: UpdatePortalProfileRequest): Observable<PortalProfile> {
    return this.http.put<PortalProfile>('/api/portal/profile', request);
  }

  listTickets(): Observable<readonly PortalTicketListItem[]> {
    return this.http.get<readonly PortalTicketListItem[]>('/api/portal/tickets');
  }

  getTicket(id: string): Observable<PortalTicketDetail> {
    return this.http.get<PortalTicketDetail>(`/api/portal/tickets/${id}`);
  }

  submitTicket(request: CreatePortalTicketRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>('/api/portal/tickets', request);
  }

  reply(id: string, body: string): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`/api/portal/tickets/${id}/reply`, { body });
  }

  submitSurvey(id: string, request: SubmitSurveyRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`/api/portal/tickets/${id}/survey`, request);
  }

  /** The submit form's category picker (US-411, PJ-13). The same seeded list the staff surface uses. */
  listCategories(): Observable<readonly CategoryOption[]> {
    return this.http.get<readonly CategoryOption[]>('/api/Categories');
  }

  /** Lists the signed-in customer's ticket attachments (TA-7). */
  listTicketAttachments(id: string): Observable<readonly PortalTicketAttachment[]> {
    return this.http.get<readonly PortalTicketAttachment[]>(`/api/portal/tickets/${id}/attachments`);
  }

  /**
   * One file against the signed-in customer's own ticket (TA-1/TA-3). No manual `Content-Type`
   * header — multipart carries a browser-generated boundary (same as the staff upload).
   */
  uploadTicketAttachment(id: string, file: File): Observable<{ id: string }> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<{ id: string }>(`/api/portal/tickets/${id}/attachments`, form);
  }

  /** The content route for a portal ticket attachment, fetched with the auth header. */
  ticketAttachmentContentUrl(id: string, attachmentId: string): string {
    return `/api/portal/tickets/${id}/attachments/${attachmentId}/content`;
  }

  /** The attachment bytes as a blob. `HttpClient` puts the auth header on; a bare URL cannot. */
  downloadTicketAttachment(id: string, attachmentId: string): Observable<Blob> {
    return this.http.get(this.ticketAttachmentContentUrl(id, attachmentId), {
      responseType: 'blob',
    });
  }
}
