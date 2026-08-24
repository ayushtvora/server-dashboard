import { Injectable, InjectionToken, inject } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Observable, Subject } from 'rxjs';
import { environment } from '../../environments/environment';
import { ServerSnapshot } from '../models/server-snapshot.model';

export type HubConnectionFactory = () => signalR.HubConnection;

function buildHubConnection(): signalR.HubConnection {
  return new signalR.HubConnectionBuilder()
    .withUrl(`${environment.apiBaseUrl}/hubs/metrics`)
    .withAutomaticReconnect()
    .build();
}

export const HUB_CONNECTION_FACTORY = new InjectionToken<HubConnectionFactory>('HUB_CONNECTION_FACTORY', {
  providedIn: 'root',
  factory: () => buildHubConnection,
});

@Injectable({ providedIn: 'root' })
export class SignalRService {
  private readonly createConnection = inject(HUB_CONNECTION_FACTORY);
  private readonly snapshotSubject = new Subject<ServerSnapshot>();
  private connection: signalR.HubConnection | null = null;

  readonly snapshots$: Observable<ServerSnapshot> = this.snapshotSubject.asObservable();

  start(): Promise<void> {
    if (this.connection) {
      return Promise.resolve();
    }

    const connection = this.createConnection();
    connection.on('snapshot', (snapshot: ServerSnapshot) => this.snapshotSubject.next(snapshot));
    this.connection = connection;
    return connection.start();
  }
}
