import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Subject } from 'rxjs';
import { vi } from 'vitest';
import { DashboardStateService } from './dashboard-state.service';
import { SignalRService } from './signalr.service';
import { ServerSnapshot } from '../models/server-snapshot.model';

function makeSnapshot(overrides: Partial<ServerSnapshot> = {}): ServerSnapshot {
  return {
    timestampUtc: '2026-08-24T00:00:00.000Z',
    serverUp: true,
    system: { cpuUsagePercent: 1, memoryUsagePercent: 2, memoryTotalMb: 3, memoryUsedMb: 4 },
    gpu: {
      available: false,
      utilizationPercent: null,
      memoryUsedMb: null,
      memoryTotalMb: null,
      temperatureCelsius: null,
    },
    containers: [],
    ...overrides,
  };
}

describe('DashboardStateService', () => {
  let httpMock: HttpTestingController;
  let service: DashboardStateService;
  let snapshots$: Subject<ServerSnapshot>;
  let start: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    snapshots$ = new Subject<ServerSnapshot>();
    start = vi.fn().mockResolvedValue(undefined);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: SignalRService, useValue: { snapshots$: snapshots$.asObservable(), start } },
      ],
    });

    httpMock = TestBed.inject(HttpTestingController);
    service = TestBed.inject(DashboardStateService);
  });

  afterEach(() => httpMock.verify());

  it('has no snapshot and safe defaults before init', () => {
    expect(service.snapshot()).toBeNull();
    expect(service.serverUp()).toBe(false);
    expect(service.system()).toBeNull();
    expect(service.gpu()).toBeNull();
    expect(service.containers()).toEqual([]);
  });

  it('populates signals from the initial HTTP seed', () => {
    service.init();

    const req = httpMock.expectOne((r) => r.url.endsWith('/api/status'));
    const seedSnapshot = makeSnapshot({ serverUp: true });
    req.flush(seedSnapshot);

    expect(service.snapshot()).toEqual(seedSnapshot);
    expect(service.serverUp()).toBe(true);
    expect(start).toHaveBeenCalledTimes(1);
  });

  it('overwrites signals when a later SignalR push arrives', () => {
    service.init();

    httpMock.expectOne((r) => r.url.endsWith('/api/status')).flush(makeSnapshot({ serverUp: true }));

    const pushedSnapshot = makeSnapshot({ serverUp: false, containers: [] });
    snapshots$.next(pushedSnapshot);

    expect(service.snapshot()).toEqual(pushedSnapshot);
    expect(service.serverUp()).toBe(false);
  });

  it('keeps a SignalR push that arrives before the HTTP seed resolves (last write wins)', () => {
    service.init();

    const req = httpMock.expectOne((r) => r.url.endsWith('/api/status'));
    const pushedSnapshot = makeSnapshot({ serverUp: false });
    snapshots$.next(pushedSnapshot);

    req.flush(makeSnapshot({ serverUp: true }));

    expect(service.snapshot()).toEqual(makeSnapshot({ serverUp: true }));
  });
});
