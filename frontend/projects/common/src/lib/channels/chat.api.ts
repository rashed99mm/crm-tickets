import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { PagedResult } from '../api/api-response';
import {
  ChatMessageDto,
  ChatReplySuggestionDto,
  ChatSessionDto,
  StartChatSessionRequest,
  StartChatSessionResponse,
} from './chat.model';

export interface ChatFilters {
  readonly page?: number;
  readonly pageSize?: number;
  readonly status?: string;
  readonly search?: string;
  readonly sortBy?: string;
  readonly sortDirection?: string;
}

@Injectable({ providedIn: 'root' })
export class ChatApi {
  private readonly http = inject(HttpClient);

  getWaitingSessions(filters: ChatFilters = {}): Observable<PagedResult<ChatSessionDto>> {
    let params = new HttpParams();
    if (filters.page !== undefined) params = params.set('page', String(filters.page));
    if (filters.pageSize !== undefined) params = params.set('pageSize', String(filters.pageSize));
    if (filters.status) params = params.set('status', filters.status);
    if (filters.search) params = params.set('search', filters.search);
    if (filters.sortBy) params = params.set('sortBy', filters.sortBy);
    if (filters.sortDirection) params = params.set('sortDirection', filters.sortDirection);
    return this.http.get<PagedResult<ChatSessionDto>>('/api/chat/waiting', { params });
  }

  claimSession(sessionId: string): Observable<ChatSessionDto> {
    return this.http.post<ChatSessionDto>(`/api/chat/sessions/${sessionId}/claim`, {});
  }

  getSessionTranscript(sessionId: string): Observable<ChatMessageDto[]> {
    return this.http.get<ChatMessageDto[]>(`/api/chat/sessions/${sessionId}/messages`);
  }

  sendMessage(sessionId: string, body: string): Observable<ChatMessageDto> {
    return this.http.post<ChatMessageDto>(`/api/chat/sessions/${sessionId}/messages`, { body });
  }

  closeSession(sessionId: string): Observable<void> {
    return this.http.post<void>(`/api/chat/sessions/${sessionId}/close`, {});
  }

  suggestReply(sessionId: string): Observable<ChatReplySuggestionDto> {
    return this.http.post<ChatReplySuggestionDto>(`/api/chat/sessions/${sessionId}/ai/reply`, {});
  }

  startAnonymousSession(request: StartChatSessionRequest): Observable<StartChatSessionResponse> {
    return this.http.post<StartChatSessionResponse>('/api/external/chat/start', request);
  }

  sendAnonymousMessage(token: string, body: string): Observable<ChatMessageDto> {
    return this.http.post<ChatMessageDto>('/api/external/chat/messages', { token, body });
  }

  getAnonymousTranscript(token: string): Observable<ChatMessageDto[]> {
    return this.http.get<ChatMessageDto[]>(
      `/api/external/chat/messages?token=${encodeURIComponent(token)}`,
    );
  }
}
