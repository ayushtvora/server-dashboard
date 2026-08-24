import { Component, input } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { ContainerStats } from '../../models/server-snapshot.model';

@Component({
  imports: [DecimalPipe],
  selector: 'app-docker-containers-card',
  styleUrl: './docker-containers-card.css',
  templateUrl: './docker-containers-card.html',
})
export class DockerContainersCard {
  readonly containers = input<ContainerStats[]>([]);
}
