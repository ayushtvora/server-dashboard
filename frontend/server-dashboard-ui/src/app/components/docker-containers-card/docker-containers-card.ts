import { Component, computed, input, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { ContainerStats } from '../../models/server-snapshot.model';
import { ContainerSortColumn, SortDirection, sortContainers } from '../../util/sort-containers';

const DEFAULT_DIRECTION: Record<ContainerSortColumn, SortDirection> = {
  name: 'asc',
  image: 'asc',
  state: 'asc',
  cpu: 'desc',
  ram: 'desc',
};

@Component({
  imports: [DecimalPipe],
  selector: 'app-docker-containers-card',
  styleUrl: './docker-containers-card.css',
  templateUrl: './docker-containers-card.html',
})
export class DockerContainersCard {
  readonly containers = input<ContainerStats[]>([]);

  readonly sortColumn = signal<ContainerSortColumn | null>(null);
  readonly sortDirection = signal<SortDirection>('asc');

  readonly sortedContainers = computed(() => {
    const column = this.sortColumn();
    return column === null ? this.containers() : sortContainers(this.containers(), column, this.sortDirection());
  });

  setSort(column: ContainerSortColumn): void {
    if (this.sortColumn() === column) {
      this.sortDirection.set(this.sortDirection() === 'asc' ? 'desc' : 'asc');
    } else {
      this.sortColumn.set(column);
      this.sortDirection.set(DEFAULT_DIRECTION[column]);
    }
  }

  ariaSortFor(column: ContainerSortColumn): 'ascending' | 'descending' | 'none' {
    if (this.sortColumn() !== column) {
      return 'none';
    }
    return this.sortDirection() === 'asc' ? 'ascending' : 'descending';
  }
}
