import { Component, input } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { SystemStats } from '../../models/server-snapshot.model';

@Component({
  imports: [DecimalPipe],
  selector: 'app-cpu-ram-card',
  styleUrl: './cpu-ram-card.css',
  templateUrl: './cpu-ram-card.html',
})
export class CpuRamCard {
  readonly system = input<SystemStats | null>(null);
}
