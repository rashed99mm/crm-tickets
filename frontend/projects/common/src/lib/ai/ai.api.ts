import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

/**
 * FEAT-21 frontend — the typed AI client. Unwrapping is the envelope interceptor's job; this
 * file stays plain routes and shapes, like every other api in the folder.
 *
 * Degraded deployments answer 503/ERR052 from these calls; callers treat that as "feature off"
 * (see the AiPanel) rather than retrying.
 */
export interface AiSuggestionDto {
  readonly id: string;
  readonly kind: 'Summary' | 'Categories' | 'Reply' | 'Solutions';
  readonly payload: unknown;
  readonly status: 'Pending' | 'Accepted' | 'Rejected';
  readonly edited: boolean;
}

/** AC-21.11 — the summary payload shape. Sentiment may be null when the model did not return a parseable label. */
export interface AiSummaryPayload {
  readonly text: string;
  readonly sentiment: 'Frustrated' | 'Neutral' | 'Satisfied' | null;
}

/** AC-21.12 — the reply payload shape. The composer toolbar uses drafts[0]; the card lists all entries. */
export interface AiReplyPayload {
  readonly drafts: readonly string[];
}

/** AC-21.13 — the categories payload shape. */
export interface AiCategoriesPayload {
  readonly options: readonly { name: string }[];
}

/** AC-21.14 — the solutions payload shape. */
export interface AiSolutionsPayload {
  readonly articles: readonly { id: string; title: string }[];
}

export interface KbCitationDto {
  readonly articleId: string;
  readonly title: string;
}

export interface AiAnswerDto {
  readonly answer: string;
  readonly citations: readonly KbCitationDto[];
}

@Injectable({ providedIn: 'root' })
export class AiApi {
  private readonly http = inject(HttpClient);

  summarise(ticketId: string): Observable<AiSuggestionDto> {
    return this.http.post<AiSuggestionDto>(`/api/Tickets/${ticketId}/ai/summary`, {});
  }

  suggestCategories(ticketId: string): Observable<AiSuggestionDto> {
    return this.http.post<AiSuggestionDto>(`/api/Tickets/${ticketId}/ai/categories`, {});
  }

  draftReply(ticketId: string, instruction?: string): Observable<AiSuggestionDto> {
    return this.http.post<AiSuggestionDto>(`/api/Tickets/${ticketId}/ai/reply`, { instruction });
  }

  suggestSolutions(ticketId: string): Observable<AiSuggestionDto> {
    return this.http.post<AiSuggestionDto>(`/api/Tickets/${ticketId}/ai/solutions`, {});
  }

  resolve(
    ticketId: string,
    suggestionId: string,
    action: 'accept' | 'reject',
    editedPayload?: string,
  ): Observable<AiSuggestionDto> {
    return this.http.post<AiSuggestionDto>(
      `/api/Tickets/${ticketId}/ai/suggestions/${suggestionId}`,
      { action, editedPayload },
    );
  }

  list(ticketId: string): Observable<readonly AiSuggestionDto[]> {
    return this.http.get<readonly AiSuggestionDto[]>(`/api/Tickets/${ticketId}/ai/suggestions`);
  }

  ask(question: string): Observable<AiAnswerDto> {
    return this.http.post<AiAnswerDto>('/api/knowledge-base/ask', { question });
  }
}
