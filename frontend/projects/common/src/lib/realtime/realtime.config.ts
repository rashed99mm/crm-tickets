import { InjectionToken } from '@angular/core';

export interface RealtimeConfig {
  /**
   * Null disables realtime entirely. That is the current state: the backend
   * has no SignalR hub yet, and no S1 acceptance criterion needs one.
   */
  readonly hubUrl: string | null;
}

export const REALTIME_HUB_PATH = '/hubs/main';
export const REALTIME_NOTIFICATION_EVENT = 'NotificationReceived';

/**
 * Has a factory default so an app that never configures realtime still
 * resolves RealtimeService rather than failing dependency injection.
 */
export const REALTIME_CONFIG = new InjectionToken<RealtimeConfig>('REALTIME_CONFIG', {
  factory: () => ({ hubUrl: null }),
});
