import { describe, expect, it } from 'vitest';
import { sortContainers } from './sort-containers';
import { ContainerStats } from '../models/server-snapshot.model';

function makeContainer(overrides: Partial<ContainerStats>): ContainerStats {
  return {
    id: overrides.id ?? 'id',
    name: overrides.name ?? 'name',
    image: overrides.image ?? 'image',
    state: overrides.state ?? 'running',
    status: overrides.status ?? 'Up 1 hour',
    createdAtUtc: overrides.createdAtUtc ?? '2026-09-04T00:00:00Z',
    cpuUsagePercent: overrides.cpuUsagePercent ?? 0,
    memoryUsageMb: overrides.memoryUsageMb ?? 0,
  };
}

const NOW = new Date('2026-09-04T12:00:00Z');

describe('sortContainers', () => {
  it('sorts by name ascending (A-Z) and descending (Z-A)', () => {
    const containers = [makeContainer({ name: 'plex' }), makeContainer({ name: 'adguard' }), makeContainer({ name: 'zigbee2mqtt' })];

    expect(sortContainers(containers, 'name', 'asc', NOW).map((c) => c.name)).toEqual(['adguard', 'plex', 'zigbee2mqtt']);
    expect(sortContainers(containers, 'name', 'desc', NOW).map((c) => c.name)).toEqual(['zigbee2mqtt', 'plex', 'adguard']);
  });

  it('sorts by image ascending (A-Z) and descending (Z-A)', () => {
    const containers = [makeContainer({ image: 'nginx:latest' }), makeContainer({ image: 'adguard/adguardhome' })];

    expect(sortContainers(containers, 'image', 'asc', NOW).map((c) => c.image)).toEqual(['adguard/adguardhome', 'nginx:latest']);
    expect(sortContainers(containers, 'image', 'desc', NOW).map((c) => c.image)).toEqual(['nginx:latest', 'adguard/adguardhome']);
  });

  it('sorts by state ascending: running > restarting > paused > created > exited > dead', () => {
    const containers = [
      makeContainer({ name: 'a', state: 'dead' }),
      makeContainer({ name: 'b', state: 'running' }),
      makeContainer({ name: 'c', state: 'exited' }),
      makeContainer({ name: 'd', state: 'created' }),
      makeContainer({ name: 'e', state: 'paused' }),
      makeContainer({ name: 'f', state: 'restarting' }),
    ];

    expect(sortContainers(containers, 'state', 'asc', NOW).map((c) => c.state)).toEqual([
      'running',
      'restarting',
      'paused',
      'created',
      'exited',
      'dead',
    ]);
  });

  it('reverses the full state order when descending', () => {
    const containers = [
      makeContainer({ name: 'a', state: 'running' }),
      makeContainer({ name: 'b', state: 'dead' }),
      makeContainer({ name: 'c', state: 'paused' }),
    ];

    expect(sortContainers(containers, 'state', 'desc', NOW).map((c) => c.state)).toEqual(['dead', 'paused', 'running']);
  });

  it('sorts unknown states after all known states', () => {
    const containers = [makeContainer({ name: 'a', state: 'removing' }), makeContainer({ name: 'b', state: 'dead' })];

    expect(sortContainers(containers, 'state', 'asc', NOW).map((c) => c.name)).toEqual(['b', 'a']);
  });

  it('breaks state ties by uptime, longest-running first, when ascending', () => {
    const containers = [
      makeContainer({ name: 'newer', state: 'running', createdAtUtc: '2026-09-04T11:00:00Z' }),
      makeContainer({ name: 'older', state: 'running', createdAtUtc: '2026-09-01T00:00:00Z' }),
    ];

    expect(sortContainers(containers, 'state', 'asc', NOW).map((c) => c.name)).toEqual(['older', 'newer']);
  });

  it('flips the state uptime tie-break to shortest-running first when descending', () => {
    const containers = [
      makeContainer({ name: 'older', state: 'running', createdAtUtc: '2026-09-01T00:00:00Z' }),
      makeContainer({ name: 'newer', state: 'running', createdAtUtc: '2026-09-04T11:00:00Z' }),
    ];

    expect(sortContainers(containers, 'state', 'desc', NOW).map((c) => c.name)).toEqual(['newer', 'older']);
  });

  it('sorts by cpu ascending (lowest first) and descending (highest first)', () => {
    const containers = [
      makeContainer({ name: 'a', cpuUsagePercent: 12.5 }),
      makeContainer({ name: 'b', cpuUsagePercent: 0.4 }),
      makeContainer({ name: 'c', cpuUsagePercent: 99.9 }),
    ];

    expect(sortContainers(containers, 'cpu', 'asc', NOW).map((c) => c.name)).toEqual(['b', 'a', 'c']);
    expect(sortContainers(containers, 'cpu', 'desc', NOW).map((c) => c.name)).toEqual(['c', 'a', 'b']);
  });

  it('sorts by ram ascending (lowest first) and descending (highest first)', () => {
    const containers = [
      makeContainer({ name: 'a', memoryUsageMb: 512 }),
      makeContainer({ name: 'b', memoryUsageMb: 64 }),
      makeContainer({ name: 'c', memoryUsageMb: 2048 }),
    ];

    expect(sortContainers(containers, 'ram', 'asc', NOW).map((c) => c.name)).toEqual(['b', 'a', 'c']);
    expect(sortContainers(containers, 'ram', 'desc', NOW).map((c) => c.name)).toEqual(['c', 'a', 'b']);
  });

  it('does not mutate the input array', () => {
    const containers = [makeContainer({ name: 'b' }), makeContainer({ name: 'a' })];
    const original = [...containers];

    sortContainers(containers, 'name', 'asc', NOW);

    expect(containers).toEqual(original);
  });
});
