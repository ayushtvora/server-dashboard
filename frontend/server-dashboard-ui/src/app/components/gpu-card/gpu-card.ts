import { Component, input } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { GpuStats } from '../../models/server-snapshot.model';

@Component({
  imports: [DecimalPipe],
  selector: 'app-gpu-card',
  styleUrl: './gpu-card.css',
  templateUrl: './gpu-card.html',
})
export class GpuCard {
  readonly gpu = input<GpuStats | null>(null);
}
