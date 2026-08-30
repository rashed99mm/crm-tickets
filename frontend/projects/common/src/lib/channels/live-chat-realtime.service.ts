import { computed, effect, inject, Injectable, NgZone, signal } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { ChatMessageDto } from './chat.model';

export type LiveChatConnectionState =
  | 'disconnected'
  | 'connecting'
  | 'connected'
  | 'reconnecting';

export const LIVE_CHAT_HUB_PATH = '/hubs/chat';
export const LIVE_CHAT_RECEIVE_EVENT = 'ChatMessageReceived';

/**
 * The anonymous hub URL for a session. Deliberately carries nothing but the opaque session token —
 * no customer id, ticket id or sender id (FB-8).
 */
export function liveChatHubUrl(sessionToken: string): string {
  return `${LIVE_CHAT_HUB_PATH}?token=${encodeURIComponent(sessionToken)}`;
}

/**
 * The anonymous live-chat SignalR client (portal widget only).
 *
 * FEAT-26 cross-host delivery: the backend publishes a `ChatMessagePushed` per message and each host
 * pushes `ChatMessageReceived` to the `chat:{sessionId}` group of its own `/hubs/chat`. This small
 * client connects the visitor to that anonymous hub with nothing but the opaque session token —
 * never a customer/ticket id (FB-8) — and forwards each push into `incoming`. It is deliberately NOT
 * the shared `RealtimeService`, which is the authenticated `/hubs/main` client and is documented as
 * non-reusable for anonymous chat (communication-channels-frontend-design:105 "small anonymous
 * SignalR client; do not loosen /hubs/main").
 */
@Injectable({ providedIn: 'root' })
export class LiveChatRealtimeService {
  private readonly zone = inject(NgZone);

  private readonly _state = signal<LiveChatConnectionState>('disconnected');
  private readonly _incoming = signal<ChatMessageDto | null>(null);

  private connection: HubConnection | null = null;
  private sessionToken: string | null = null;
  private retryTimer: ReturnType<typeof setTimeout> | null = null;

  readonly state = this._state.asReadonly();
  readonly incoming = this._incoming.asReadonly();
  readonly isConnected = computed(() => this._state() === 'connected');

  constructor() {
    // When the widget ends the chat (or the app tells the client to go away) with no token, ensure
    // any lingering connection is stopped rather than left reconnecting in the background.
    effect(() => {
      if (!this.sessionToken) {
        void this.disconnect();
      }
    });
  }

  /**
   * Connects (or switches) the client to `/hubs/chat?token=<sessionToken>` so the visitor receives
   * `ChatMessageReceived` pushes scoped to that session. Idempotent for the same token.
   */
  async connect(sessionToken: string): Promise<void> {
    if (this.sessionToken === sessionToken && this.connection) {
      return;
    }

    this.sessionToken = sessionToken;
    this._state.set('connecting');

    let connection: HubConnection | null = null;
    try {
      connection = new HubConnectionBuilder()
        .withUrl(liveChatHubUrl(sessionToken))
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Warning)
        .build();

      connection.onreconnecting(() => this.zone.run(() => this._state.set('reconnecting')));
      connection.onreconnected(() => this.zone.run(() => this._state.set('connected')));
      connection.onclose(() =>
        this.zone.run(() => {
          this._state.set('disconnected');
          if (this.connection === connection && this.sessionToken === sessionToken) {
            this.connection = null;
            this.scheduleRetry(sessionToken);
          }
        }),
      );
      connection.on(LIVE_CHAT_RECEIVE_EVENT, (payload: unknown) =>
        this.zone.run(() => this._incoming.set(toChatMessageDto(payload))),
      );

      this.connection = connection;

      await connection.start();
      this._state.set('connected');
    } catch {
      // Automatic reconnect starts only after a successful handshake. Retry the initial handshake
      // too, so starting the chat while the API is restarting does not require a page refresh.
      if (connection && this.connection === connection && this.sessionToken === sessionToken) {
        this.connection = null;
        this._state.set('disconnected');
        this.scheduleRetry(sessionToken);
      }
    }
  }

  private scheduleRetry(sessionToken: string): void {
    if (this.retryTimer || this.sessionToken !== sessionToken) {
      return;
    }

    this.retryTimer = setTimeout(() => {
      this.retryTimer = null;
      if (this.sessionToken === sessionToken) {
        void this.connect(sessionToken);
      }
    }, 3000);
  }

  /** Stops the connection and clears session context. Safe to call when never connected. */
  async disconnect(): Promise<void> {
    if (this.retryTimer) {
      clearTimeout(this.retryTimer);
      this.retryTimer = null;
    }

    const connection = this.connection;
    this.connection = null;
    if (connection && connection.state !== HubConnectionState.Disconnected) {
      await connection.stop();
    }
    this.sessionToken = null;
    this._incoming.set(null);
    this._state.set('disconnected');
  }
}

function toChatMessageDto(payload: unknown): ChatMessageDto {
  const p = payload as Record<string, unknown>;
  return {
    id: String(p['id']),
    sessionId: String(p['sessionId']),
    senderType: p['senderType'] === 'Agent' ? 'Agent' : p['senderType'] === 'System' ? 'System' : 'Customer',
    senderName: String(p['senderName']),
    senderId: (p['senderId'] as string | null) ?? undefined,
    body: String(p['body']),
    sentAt: String(p['sentAt']),
  };
}
