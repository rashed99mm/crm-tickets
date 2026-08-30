import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { KbCitationDto } from './ai.api';

/** One rendered turn of an AI conversation (AI-47). */
export interface AiChatTurnDto {
  readonly id: string;
  readonly role: 'user' | 'assistant';
  readonly body: string;
  readonly citations: readonly KbCitationDto[];
}

/** A conversation as the backend renders it. */
export interface AiChatDto {
  readonly sessionId: string;
  readonly status: 'Open' | 'Closed';
  readonly ticketId: string | null;
  readonly turns: readonly AiChatTurnDto[];
}

/**
 * The multi-turn chat client (AI-38..AI-43). The same routes serve the staff shell and the
 * portal — the host in the URL decides the scope, and the bearer token decides the actor, so
 * this file stays identical for both surfaces.
 */
@Injectable({ providedIn: 'root' })
export class AiChatApi {
  private readonly http = inject(HttpClient);

  start(message: string): Observable<AiChatDto> {
    return this.http.post<AiChatDto>('/api/ai/chats', { message });
  }

  send(sessionId: string, message: string): Observable<AiChatDto> {
    return this.http.post<AiChatDto>(`/api/ai/chats/${sessionId}/messages`, { message });
  }

  get(sessionId: string): Observable<AiChatDto> {
    return this.http.get<AiChatDto>(`/api/ai/chats/${sessionId}`);
  }

  /** Staff-only in practice; harmless from the portal because the backend scopes it. */
  handoff(sessionId: string, customerId?: string, categoryId?: string): Observable<string> {
    return this.http.post<string>(`/api/ai/chats/${sessionId}/handoff`, { customerId, categoryId });
  }
}
