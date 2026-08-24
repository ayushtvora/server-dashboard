import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { ContainerStats, GpuStats, ServerSnapshot, SystemStats } from '../models/server-snapshot.model';
import { SignalRService } from './signalr.service';

@Injectable({ providedIn: 'root' })
export class DashboardStateService {
  private readonly http = inject(HttpClient);
  private readonly signalR = inject(SignalRService);

  private readonly snapshotSignal = signal<ServerSnapshot | null>(null);
  readonly snapshot = this.snapshotSignal.asReadonly();

  readonly serverUp = computed<boolean>(() => this.snapshotSignal()?.serverUp ?? false);
  readonly system = computed<SystemStats | null>(() => this.snapshotSignal()?.system ?? null);
  readonly gpu = computed<GpuStats | null>(() => this.snapshotSignal()?.gpu ?? null);
  readonly containers = computed<ContainerStats[]>(() => this.snapshotSignal()?.containers ?? []);

  init(): void {
    this.signalR.snapshots$.subscribe((snapshot) => this.snapshotSignal.set(snapshot));

    this.http
      .get<ServerSnapshot>(`${environment.apiBaseUrl}/api/status`)
      .subscribe((snapshot) => this.snapshotSignal.set(snapshot));

    void this.signalR.start();
  }
}
