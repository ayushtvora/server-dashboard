import { Component, computed, input } from '@angular/core';
import { formatUptime } from '../../util/format-uptime';

@Component({
  imports: [],
  selector: 'app-server-status-badge',
  styleUrl: './server-status-badge.css',
  templateUrl: './server-status-badge.html',
})
export class ServerStatusBadge {
  readonly serverUp = input.required<boolean>();
  readonly uptimeSeconds = input<number | null>(null);

  readonly uptimeLabel = computed(() => {
    const seconds = this.uptimeSeconds();
    return seconds === null ? null : formatUptime(seconds);
  });
}
