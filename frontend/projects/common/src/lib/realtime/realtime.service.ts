import { computed, effect, inject, Injectable, NgZone, signal } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { REALTIME_CONFIG, REALTIME_NOTIFICATION_EVENT } from './realtime.config';
import { SessionStore } from '../auth/session.store';
import { NotificationStore } from '../notifications/notification.store';
import { InAppPushPayload, toInAppNotification } from '../notifications/notification.model';

export type ConnectionState =
  | 'disconnected'
  | 'connecting'
  | 'connected'
  | 'reconnecting';

/**
 * SignalR client. Inert unless a hub url is configured (REALTIME_CONFIG).
 *
 * FEAT-15: when enabled and the user is authenticated, it connects to the hub with the live
 * access token and forwards `NotificationReceived` pushes into `NotificationStore`. The backend
 * subscribes each connection to `user:{userId}` on connect, so the client never calls `JoinGroup`.
 *
 * Every method stays safe to call with no connection rather than throwing — an app that logged a
 * connection failure on every boot would train everyone to ignore its logs.
 */
@Injectable({ providedIn: 'root' })
export class RealtimeService {
  private readonly config = inject(REALTIME_CONFIG);
  private readonly session = inject(SessionStore);
  private readonly store = inject(NotificationStore);
  private readonly zone = inject(NgZone);
  private readonly _state = signal<ConnectionState>('disconnected');

  private connection: HubConnection | null = null;
  private retryTimer: ReturnType<typeof setTimeout> | null = null;

  /** Handlers registered before start, replayed onto the connection. */
  private readonly pending = new Map<string, (payload: never) => void>();

  readonly connectionState = this._state.asReadonly();
  readonly isConnected = computed(() => this._state() === 'connected');

  get isEnabled(): boolean {
    return this.config.hubUrl !== null;
  }

  constructor() {
    // Start when enabled + authenticated, stop (and clear the inbox) otherwise.
    effect(() => {
      if (this.config.hubUrl !== null && this.session.isAuthenticated()) {
        void this.start();
      } else {
        void this.stop();
      }
    });
  }

  async start(): Promise<void> {
    if (!this.config.hubUrl || this.connection) {
      return;
    }

    this._state.set('connecting');

    const connection = new HubConnectionBuilder()
      .withUrl(this.config.hubUrl, { accessTokenFactory: () => this.session.token() ?? '' })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.onreconnecting(() => this._state.set('reconnecting'));
    connection.onreconnected(() => this._state.set('connected'));
    connection.onclose(() => {
      this._state.set('disconnected');
      if (this.connection === connection) {
        this.connection = null;
        this.scheduleRetry();
      }
    });
    connection.on(REALTIME_NOTIFICATION_EVENT, (p: InAppPushPayload) =>
      this.zone.run(() => this.store.add(toInAppNotification(p))),
    );

    // Subscriptions made before start still have to fire, or call order
    // silently determines which events a component receives.
    for (const [event, handler] of this.pending) {
      connection.on(event, handler);
    }

    this.connection = connection;

    try {
      await connection.start();
      this._state.set('connected');
    } catch {
      // SignalR automatic reconnect only applies after a successful handshake. Retry the
      // initial handshake as well, otherwise a short API restart leaves the app permanently
      // disconnected until the user reloads the page.
      if (this.connection === connection) {
        this.connection = null;
        this._state.set('disconnected');
        this.scheduleRetry();
      }
    }
  }

  private scheduleRetry(): void {
    if (this.retryTimer || !this.config.hubUrl || !this.session.isAuthenticated()) {
      return;
    }

    this.retryTimer = setTimeout(() => {
      this.retryTimer = null;
      void this.start();
    }, 3000);
  }

  private cancelRetry(): void {
    if (this.retryTimer) {
      clearTimeout(this.retryTimer);
      this.retryTimer = null;
    }
  }

  async stop(): Promise<void> {
    this.cancelRetry();
    const connection = this.connection;
    this.connection = null;

    if (connection && connection.state !== HubConnectionState.Disconnected) {
      await connection.stop();
    }

    this._state.set('disconnected');
    this.store.clear();
  }

  on<T>(event: string, handler: (payload: T) => void): () => void {
    this.pending.set(event, handler as (payload: never) => void);
    this.connection?.on(event, handler);
    // Returning the unregister closure lets scoped stores (e.g. ChatStore) detach their handler
    // on teardown instead of leaking it across sessions.
    return () => {
      this.pending.delete(event);
      this.connection?.off(event, handler);
    };
  }
}
