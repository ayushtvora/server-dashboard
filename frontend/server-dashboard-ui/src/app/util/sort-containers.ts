import { ContainerStats } from '../models/server-snapshot.model';

export type ContainerSortColumn = 'name' | 'image' | 'state' | 'cpu' | 'ram';
export type SortDirection = 'asc' | 'desc';

// Best (0) to worst (higher). Unrecognized states sort after all known ones.
const STATE_RANK: Record<string, number> = {
  running: 0,
  restarting: 1,
  paused: 2,
  created: 3,
  exited: 4,
  dead: 5,
};
const UNKNOWN_STATE_RANK = Object.keys(STATE_RANK).length;

function stateRank(state: string): number {
  return STATE_RANK[state.toLowerCase()] ?? UNKNOWN_STATE_RANK;
}

function uptimeSeconds(container: ContainerStats, nowMs: number): number {
  return (nowMs - new Date(container.createdAtUtc).getTime()) / 1000;
}

// Comparators are written for ascending order; `sortContainers` negates the
// result for descending. For "state", ascending means best-to-worst, tied
// states broken by longest-uptime-first (so descending flips both).
function compareAscending(a: ContainerStats, b: ContainerStats, column: ContainerSortColumn, nowMs: number): number {
  switch (column) {
    case 'name':
      return a.name.localeCompare(b.name);
    case 'image':
      return a.image.localeCompare(b.image);
    case 'state': {
      const rankDiff = stateRank(a.state) - stateRank(b.state);
      return rankDiff !== 0 ? rankDiff : uptimeSeconds(b, nowMs) - uptimeSeconds(a, nowMs);
    }
    case 'cpu':
      return a.cpuUsagePercent - b.cpuUsagePercent;
    case 'ram':
      return a.memoryUsageMb - b.memoryUsageMb;
  }
}

export function sortContainers(
  containers: readonly ContainerStats[],
  column: ContainerSortColumn,
  direction: SortDirection,
  now: Date = new Date(),
): ContainerStats[] {
  const nowMs = now.getTime();
  const sign = direction === 'asc' ? 1 : -1;

  return [...containers].sort((a, b) => sign * compareAscending(a, b, column, nowMs));
}
