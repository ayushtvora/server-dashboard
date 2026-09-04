import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { vi } from 'vitest';
import { HUB_CONNECTION_FACTORY, SignalRService } from './signalr.service';
import { ServerSnapshot } from '../models/server-snapshot.model';

class FakeHubConnection {
  started = false;
  private readonly handlers = new Map<string, (payload: unknown) => void>();

  on(event: string, handler: (payload: unknown) => void): void {
    this.handlers.set(event, handler);
  }

  start(): Promise<void> {
    this.started = true;
    return Promise.resolve();
  }

  emit(event: string, payload: unknown): void {
    this.handlers.get(event)?.(payload);
  }
}

const sampleSnapshot: ServerSnapshot = {
  timestampUtc: '2026-08-24T00:00:00.000Z',
  serverUp: true,
  system: {
    cpuUsagePercent: 10,
    memoryUsagePercent: 20,
    memoryTotalMb: 1000,
    memoryUsedMb: 200,
    cpuTemperatureCelsius: 45,
    uptimeSeconds: 3600,
  },
  gpu: {
    available: false,
    utilizationPercent: null,
    memoryUsedMb: null,
    memoryTotalMb: null,
    temperatureCelsius: null,
  },
  containers: [],
};

describe('SignalRService', () => {
  let fakeConnection: FakeHubConnection;
  let factory: ReturnType<typeof vi.fn<() => FakeHubConnection>>;
  let service: SignalRService;

  beforeEach(() => {
    fakeConnection = new FakeHubConnection();
    factory = vi.fn(() => fakeConnection);

    TestBed.configureTestingModule({
      providers: [{ provide: HUB_CONNECTION_FACTORY, useValue: factory }],
    });

    service = TestBed.inject(SignalRService);
  });

  it('starts the underlying hub connection', async () => {
    await service.start();
    expect(fakeConnection.started).toBe(true);
  });

  it('only creates one connection even if start is called multiple times', async () => {
    await service.start();
    await service.start();
    expect(factory).toHaveBeenCalledTimes(1);
  });

  it('emits incoming "snapshot" events on snapshots$', async () => {
    const received = firstValueFrom(service.snapshots$);
    await service.start();
    fakeConnection.emit('snapshot', sampleSnapshot);
    await expect(received).resolves.toEqual(sampleSnapshot);
  });
});
