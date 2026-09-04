// Compact uptime string, e.g. "3d 4h 12m" / "4h" / "42m" / "38s".
// Drops seconds once minutes are shown, and drops any zero-value unit within
// the displayed range (e.g. "3d 12m" when hours happen to be zero).
export function formatUptime(totalSeconds: number): string {
  const wholeSeconds = Math.floor(totalSeconds);
  const days = Math.floor(wholeSeconds / 86400);
  const hours = Math.floor((wholeSeconds % 86400) / 3600);
  const minutes = Math.floor((wholeSeconds % 3600) / 60);
  const seconds = wholeSeconds % 60;

  const units =
    days > 0
      ? [
          { value: days, label: 'd' },
          { value: hours, label: 'h' },
          { value: minutes, label: 'm' },
        ]
      : hours > 0
        ? [
            { value: hours, label: 'h' },
            { value: minutes, label: 'm' },
          ]
        : minutes > 0
          ? [{ value: minutes, label: 'm' }]
          : [{ value: seconds, label: 's' }];

  const shown = units.filter((unit) => unit.value > 0);
  const displayUnits = shown.length > 0 ? shown : [units[units.length - 1]];

  return displayUnits.map((unit) => `${unit.value}${unit.label}`).join(' ');
}
