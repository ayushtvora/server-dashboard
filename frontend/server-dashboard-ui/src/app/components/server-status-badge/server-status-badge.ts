import { Component, input } from '@angular/core';

@Component({
  imports: [],
  selector: 'app-server-status-badge',
  styleUrl: './server-status-badge.css',
  templateUrl: './server-status-badge.html',
})
export class ServerStatusBadge {
  readonly serverUp = input.required<boolean>();
}
