import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ContentSummary } from '../contents/contents.api';
import { PagedResult } from '../api/api-response';

/** FEAT-18 US-503 — the category tree node as `ContentCategoriesController` returns it. */
export interface CategoryNode {
  readonly id: string;
  readonly name: string;
  readonly parentId: string | null;
  readonly children: readonly CategoryNode[];
}

/** US-511 AC3 — a version-history entry, newest first as the server orders them. */
export interface ContentVersion {
  readonly versionNumber: number;
  readonly authorId: string;
  readonly changeSummary: string | null;
  readonly createdAt: string;
}

/** The staff/admin knowledge-base surface (InternalApi `/api/Contents`) — all statuses. */
@Injectable({ providedIn: 'root' })
export class KbAdminApi {
  private readonly http = inject(HttpClient);

  list(status?: string, searchTerm?: string, page = 1, pageSize = 10): Observable<PagedResult<ContentSummary>> {
    let params = new HttpParams().set('page', String(page)).set('pageSize', String(pageSize));
    if (status) {
      params = params.set('status', status);
    }
    if (searchTerm) {
      params = params.set('searchTerm', searchTerm);
    }
    return this.http.get<PagedResult<ContentSummary>>('/api/Contents', { params });
  }

  get(id: string): Observable<ContentSummary> {
    return this.http.get<ContentSummary>(`/api/Contents/${id}`);
  }

  /** US-510 AC2 — a create always lands as a Draft; the server takes the author from the token. */
  create(request: {
    title: string;
    body: string;
    summary?: string | null;
    contentType?: string;
    status?: string;
    tags?: string[];
  }): Observable<{ id: string }> {
    return this.http.post<{ id: string }>('/api/Contents', {
      contentType: 'Article',
      status: 'Draft',
      tags: [],
      ...request,
    });
  }

  /** US-511 AC2 — a save of a Draft records a new version server-side. */
  update(
    id: string,
    request: { title?: string; body?: string; summary?: string | null; tags?: string[] },
  ): Observable<unknown> {
    return this.http.put(`/api/Contents/${id}`, request);
  }

  publish(id: string): Observable<unknown> {
    return this.http.post(`/api/Contents/${id}/publish`, {});
  }

  archive(id: string): Observable<unknown> {
    return this.http.post(`/api/Contents/${id}/archive`, {});
  }

  versions(id: string): Observable<readonly ContentVersion[]> {
    return this.http.get<readonly ContentVersion[]>(`/api/Contents/${id}/versions`);
  }

  categories(): Observable<readonly CategoryNode[]> {
    return this.http.get<readonly CategoryNode[]>('/api/ContentCategories');
  }

  assignCategory(id: string, categoryId: string | null): Observable<unknown> {
    return this.http.put(`/api/Contents/${id}/category`, { categoryId });
  }

  setFaq(id: string, isFaq: boolean): Observable<unknown> {
    return this.http.put(`/api/Contents/${id}/faq`, { isFaq });
  }
}
