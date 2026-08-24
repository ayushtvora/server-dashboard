import { Component, OnInit, inject } from '@angular/core';
import { ServerStatusBadge } from './components/server-status-badge/server-status-badge';
import { CpuRamCard } from './components/cpu-ram-card/cpu-ram-card';
import { GpuCard } from './components/gpu-card/gpu-card';
import { DockerContainersCard } from './components/docker-containers-card/docker-containers-card';
import { DashboardStateService } from './services/dashboard-state.service';

@Component({
  imports: [ServerStatusBadge, CpuRamCard, GpuCard, DockerContainersCard],
  selector: 'app-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App implements OnInit {
  protected readonly state = inject(DashboardStateService);

  ngOnInit(): void {
    this.state.init();
  }
}
