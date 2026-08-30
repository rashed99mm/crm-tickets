import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../api/api-response';

/** Matches the backend's `CustomerDto`. */
export interface Customer {
  readonly id: string;
  readonly name: string;
  readonly email: string;
  readonly phone: string | null;
  readonly createdAt: string;
}

/**
 * One entry of a customer's interaction history.
 *
 * `authorName` is projected at read time from `AuthorId`, not stored on the row — the same
 * arrangement ticket history uses. The client only ever reads it.
 */
export interface CustomerNote {
  readonly id: string;
  readonly body: string;
  readonly authorId: string;
  readonly authorName: string;
  readonly createdAt: string;
}

/**
 * One file attached to a customer, as the read model projects it.
 *
 * `originalFileName` is what the uploader called the file; it is **not** what sits on disk. The
 * stored name is a server-generated `Guid + extension` (`AC-25`), so a hostile filename never
 * reaches the filesystem. The client renders the original because that is the name the agent
 * recognises, and it is only ever text.
 */
export interface CustomerAttachment {
  readonly id: string;
  readonly originalFileName: string;
  readonly contentType: string;
  readonly sizeBytes: number;
  readonly uploadedByName: string;
  readonly createdAt: string;
}

/**
 * Mirrors the server's limits so the client can refuse early (`AC-84`).
 *
 * **The server refuses independently.** `AC-23` and `AC-24` are server criteria and stay server
 * criteria; these constants exist only so an agent who picks a 40 MB video is told immediately
 * instead of after uploading it. Treating this as the control would put the limit somewhere the
 * user can edit.
 */
export const MAX_ATTACHMENT_BYTES = 10 * 1024 * 1024;

/**
 * An allowlist, not a blocklist — a blocklist is a list of the attacks someone already thought of
 * (assumption `A20`). Kept in the same order as the server's.
 */
export const ALLOWED_ATTACHMENT_TYPES = [
  'image/png',
  'image/jpeg',
  'image/gif',
  'application/pdf',
  'text/plain',
] as const;

export interface CustomerFilters {
  readonly page?: number;
  readonly pageSize?: number;
  readonly search?: string;
}

/** The create and update payloads. Identical by criterion — AC-14 says update's rules are AC-8's. */
export interface CustomerRequest {
  readonly name: string;
  readonly email: string;
  readonly phone: string | null;
}

/**
 * Customer calls, including the interaction history.
 *
 * Catches nothing: failures arrive as `ApiError` from the envelope interceptor. A service that
 * swallowed them would be the first step towards rendering a server fault as "no customers", which
 * is exactly what AC-69's three-distinct-states clause forbids.
 */
@Injectable({ providedIn: 'root' })
export class CustomerApi {
  private readonly http = inject(HttpClient);

  list(filters: CustomerFilters = {}): Observable<PagedResult<Customer>> {
    let params = new HttpParams()
      .set('page', String(filters.page ?? 1))
      .set('pageSize', String(filters.pageSize ?? 10));

    // Absent rather than empty: `search=` describes a request that was never made, and it is the
    // difference a future server-side NotEmpty rule would turn into a 400.
    if (filters.search) {
      params = params.set('search', filters.search);
    }

    return this.http.get<PagedResult<Customer>>('/api/Customers', { params });
  }

  get(id: string): Observable<Customer> {
    return this.http.get<Customer>(`/api/Customers/${id}`);
  }

  create(request: CustomerRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>('/api/Customers', request);
  }

  update(id: string, request: CustomerRequest): Observable<unknown> {
    return this.http.put(`/api/Customers/${id}`, request);
  }

  /** Soft-deletes. Refused with 409 (`CUSTOMER_HAS_TICKETS`) if the customer holds any ticket. */
  remove(id: string): Observable<unknown> {
    return this.http.delete(`/api/Customers/${id}`);
  }

  /** Newest first, as the server orders them. The client does not re-sort — see the notes component. */
  listNotes(id: string, page = 1, pageSize = 20): Observable<PagedResult<CustomerNote>> {
    const params = new HttpParams().set('page', String(page)).set('pageSize', String(pageSize));
    return this.http.get<PagedResult<CustomerNote>>(`/api/Customers/${id}/notes`, { params });
  }

  /**
   * AC-76 — the note's author comes from the session, so this takes a body and nothing else.
   * There is deliberately no author parameter: a criterion enforced only by the server is one a
   * careless client can still attempt, and the narrow signature removes the attempt entirely.
   */
  addNote(id: string, body: string): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`/api/Customers/${id}/notes`, { body });
  }

  /** AC-83 — the customer's files, newest first, in the order the server returned them. */
  listAttachments(id: string, page = 1, pageSize = 20): Observable<PagedResult<CustomerAttachment>> {
    const params = new HttpParams().set('page', String(page)).set('pageSize', String(pageSize));
    return this.http.get<PagedResult<CustomerAttachment>>(`/api/Customers/${id}/attachments`, { params });
  }

  /**
   * AC-84 — one file, as `multipart/form-data`.
   *
   * **No `Content-Type` header is set here, deliberately.** Multipart carries a boundary token that
   * only the browser can generate, and setting `multipart/form-data` by hand produces a header with
   * no boundary — the server then cannot parse a body that is otherwise perfectly well formed. It
   * fails as a 400 that looks like a validation bug, so the absence is load-bearing, not an
   * omission.
   *
   * Like `addNote`, there is no uploader parameter: the actor comes from the token (`AC-22`).
   */
  uploadAttachment(id: string, file: File): Observable<{ id: string }> {
    const form = new FormData();
    // The field name the server's `IFormFile` parameter binds to.
    form.append('file', file, file.name);

    return this.http.post<{ id: string }>(`/api/Customers/${id}/attachments`, form);
  }

  /**
   * The content route. Exposed as a string so the component can name it in one place, but it is
   * **not** an href — see `downloadAttachment`.
   */
  downloadUrl(id: string, attachmentId: string): string {
    return `/api/Customers/${id}/attachments/${attachmentId}/content`;
  }

  /**
   * AC-85 — fetches the bytes through `HttpClient`.
   *
   * A plain `<a href>` would be the obvious implementation and is wrong: the content route requires
   * a session (`AC-26`), a link carries no `Authorization` header, and the download would 401 and
   * read as a broken button. Going through `HttpClient` puts the request through the auth
   * interceptor.
   *
   * The response is not an envelope, so the envelope interceptor passes it through untouched. A
   * *failure* on this route is also not an envelope — the body is a `Blob` — so it normalises to
   * `ERR_NETWORK` rather than the server's code. Acceptable: a failed download has one useful
   * message either way, and unpacking a blob to read a code would add a parse that can itself fail.
   */
  downloadAttachment(id: string, attachmentId: string): Observable<Blob> {
    return this.http.get(this.downloadUrl(id, attachmentId), { responseType: 'blob' });
  }

  /** AC-85 — soft-deletes the link, retires the asset and removes the file (`AC-28`). */
  removeAttachment(id: string, attachmentId: string): Observable<unknown> {
    return this.http.delete(`/api/Customers/${id}/attachments/${attachmentId}`);
  }
}
