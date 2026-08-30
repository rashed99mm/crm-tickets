import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../api/api-response';

/** A help article as the knowledge base list/detail show it. Mirrors `ContentDto` fields the UI uses. */
export interface ContentSummary {
  readonly id: string;
  readonly title: string;
  readonly summary: string | null;
  readonly contentType: string;
  readonly featuredImageUrl?: string | null;
  readonly status: string;
  readonly category: string | null;
  readonly categoryId?: string | null;
  readonly categoryName?: string | null;
  readonly tags: readonly string[];
  readonly viewCount: number;
  readonly likeCount: number;
  readonly dislikeCount?: number;
  readonly isFaq?: boolean;
  readonly publishedAt: string | null;
  readonly body: string;
}

/** A node in the public KB category tree — mirrors `ContentCategoryNodeDto`. */
export interface KbCategoryNode {
  readonly id: string;
  readonly name: string;
  readonly parentId: string | null;
  readonly children: readonly KbCategoryNode[];
}

/** The customer-facing knowledge base, served by the public `KnowledgeBaseController`. */
@Injectable({ providedIn: 'root' })
export class ContentsApi {
  private readonly http = inject(HttpClient);

  list(search?: string, page = 1, pageSize = 10, categoryId?: string): Observable<PagedResult<ContentSummary>> {
    let params = new HttpParams()
      .set('page', String(page))
      .set('pageSize', String(pageSize));
    if (search) {
      params = params.set('searchTerm', search);
    }
    if (categoryId) {
      params = params.set('categoryId', categoryId);
    }
    return this.http.get<PagedResult<ContentSummary>>('/api/knowledge-base/articles', { params });
  }

  get(id: string): Observable<ContentSummary> {
    return this.http.get<ContentSummary>(`/api/knowledge-base/articles/${id}`);
  }

  /** The public KB category tree — returns active categories only. */
  categories(): Observable<readonly KbCategoryNode[]> {
    return this.http.get<readonly KbCategoryNode[]>('/api/knowledge-base/categories');
  }

  /** Published FAQ articles (US-504 / US-513). Paginated, supports search. Capped at 3 by default to fit the bento layout. */
  faq(searchTerm?: string, skip = 0, take = 3): Observable<PagedResult<ContentSummary>> {
    let params = new HttpParams().set('skip', String(skip)).set('take', String(take));
    if (searchTerm) {
      params = params.set('searchTerm', searchTerm);
    }
    return this.http.get<PagedResult<ContentSummary>>('/api/knowledge-base/articles/faq', { params });
  }

  /** Record a helpfulness vote (US-508). Requires an authenticated session. */
  vote(id: string, isHelpful: boolean): Observable<unknown> {
    return this.http.post(`/api/knowledge-base/articles/${id}/vote`, { isHelpful });
  }
}
