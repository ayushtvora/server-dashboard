export interface SystemStats {
  cpuUsagePercent: number;
  memoryUsagePercent: number;
  memoryTotalMb: number;
  memoryUsedMb: number;
  cpuTemperatureCelsius: number | null;
  uptimeSeconds: number;
}

export interface GpuStats {
  available: boolean;
  utilizationPercent: number | null;
  memoryUsedMb: number | null;
  memoryTotalMb: number | null;
  temperatureCelsius: number | null;
}

export interface ContainerStats {
  id: string;
  name: string;
  image: string;
  state: string;
  status: string;
  cpuUsagePercent: number;
  memoryUsageMb: number;
}

export interface ServerSnapshot {
  timestampUtc: string;
  serverUp: boolean;
  system: SystemStats;
  gpu: GpuStats;
  containers: ContainerStats[];
}
