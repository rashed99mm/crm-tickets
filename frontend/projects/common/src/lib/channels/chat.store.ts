import { computed, inject, Injectable, signal } from '@angular/core';
import { ApiError } from '../api/api-error';
import { RealtimeService } from '../realtime/realtime.service';
import { ChatApi } from './chat.api';
import { ChatMessageDto } from './chat.model';

@Injectable({ providedIn: 'root' })
export class ChatStore {
  private readonly api = inject(ChatApi);
  private readonly realtime = inject(RealtimeService);

  private readonly _sessionId = signal<string | null>(null);
  private readonly _messages = signal<readonly ChatMessageDto[]>([]);
  private readonly _loading = signal(false);
  private readonly _sending = signal(false);
  private readonly _error = signal<ApiError | null>(null);

  readonly sessionId = this._sessionId.asReadonly();
  readonly messages = this._messages.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly sending = this._sending.asReadonly();
  readonly error = this._error.asReadonly();
  readonly hasMessages = computed(() => this._messages().length > 0);

  private unregisterRealtime?: () => void;

  initSession(sessionId: string): void {
    this._sessionId.set(sessionId);
    this._messages.set([]);
    this._error.set(null);
    this._loading.set(true);

    this.api.getSessionTranscript(sessionId).subscribe({
      next: (transcript) => {
        this._messages.set(transcript);
        this._loading.set(false);
      },
      error: (err: unknown) => {
        this._loading.set(false);
        this._error.set(
          err instanceof ApiError ? err : new ApiError('ERR_LOAD', 'Failed to load chat', [], '', 0),
        );
      },
    });

    this.unregisterRealtime?.();
    this.unregisterRealtime = this.realtime.on<ChatMessageDto>('ChatMessageReceived', (msg) => {
      if (msg.sessionId === this._sessionId()) {
        this.appendMessage(msg);
      }
    });
  }

  appendMessage(message: ChatMessageDto): void {
    const current = this._messages();
    if (!current.some((m) => m.id === message.id)) {
      this._messages.set([...current, message]);
    }
  }

  sendMessage(body: string): void {
    const sId = this._sessionId();
    if (!sId || !body.trim() || this._sending()) {
      return;
    }

    this._sending.set(true);
    this.api.sendMessage(sId, body.trim()).subscribe({
      next: (sent) => {
        this._sending.set(false);
        this.appendMessage(sent);
      },
      error: (err: unknown) => {
        this._sending.set(false);
        this._error.set(
          err instanceof ApiError ? err : new ApiError('ERR_SEND', 'Failed to send message', [], '', 0),
        );
      },
    });
  }

  destroy(): void {
    this.unregisterRealtime?.();
    this.unregisterRealtime = undefined;
    this._sessionId.set(null);
    this._messages.set([]);
  }
}
