import { TestBed } from '@angular/core/testing';
import { REALTIME_CONFIG } from './realtime.config';
import { RealtimeService } from './realtime.service';

function serviceWith(hubUrl: string | null): RealtimeService {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [RealtimeService, { provide: REALTIME_CONFIG, useValue: { hubUrl } }],
  });
  return TestBed.inject(RealtimeService);
}

describe('RealtimeService', () => {
  it('is disabled and inert with no hub url', async () => {
    // This is the state today: the backend has no hub. Starting must be a
    // no-op rather than an error, or every app boot logs a connection
    // failure and everyone learns to ignore the log.
    const service = serviceWith(null);

    expect(service.isEnabled).toBe(false);
    expect(service.connectionState()).toBe('disconnected');

    await service.start();

    expect(service.connectionState()).toBe('disconnected');
  });

  it('reports enabled when a hub url is configured', () => {
    expect(serviceWith('/hubs/tickets').isEnabled).toBe(true);
  });

  it('registers handlers with no connection and does not throw', () => {
    // Components subscribe on init, which may happen before or without a
    // connection ever being established.
    const service = serviceWith(null);

    expect(() => service.on('TicketChanged', () => {})).not.toThrow();
  });

  it('stop is safe when never started', async () => {
    const service = serviceWith(null);

    await service.stop();

    expect(service.connectionState()).toBe('disconnected');
  });

  it('defaults to disabled when no config is provided at all', () => {
    // The injection token has a factory default, so an app that never
    // configures realtime still resolves the service rather than failing DI.
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [RealtimeService] });

    expect(TestBed.inject(RealtimeService).isEnabled).toBe(false);
  });
});
