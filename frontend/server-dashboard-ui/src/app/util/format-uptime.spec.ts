import { describe, expect, it } from 'vitest';
import { formatUptime } from './format-uptime';

describe('formatUptime', () => {
  it('shows seconds only, under a minute', () => {
    expect(formatUptime(0)).toBe('0s');
    expect(formatUptime(38)).toBe('38s');
    expect(formatUptime(59)).toBe('59s');
  });

  it('shows minutes only, under an hour, dropping seconds', () => {
    expect(formatUptime(60)).toBe('1m');
    expect(formatUptime(42 * 60)).toBe('42m');
    expect(formatUptime(59 * 60 + 59)).toBe('59m');
  });

  it('shows hours and minutes, under a day, dropping seconds', () => {
    expect(formatUptime(3600)).toBe('1h');
    expect(formatUptime(4 * 3600 + 12 * 60 + 33)).toBe('4h 12m');
  });

  it('shows days, hours, and minutes, dropping seconds', () => {
    expect(formatUptime(3 * 86400 + 4 * 3600 + 12 * 60 + 33)).toBe('3d 4h 12m');
  });

  it('drops zero-value units within the displayed range', () => {
    expect(formatUptime(3 * 86400)).toBe('3d');
    expect(formatUptime(3 * 86400 + 12 * 60)).toBe('3d 12m');
    expect(formatUptime(4 * 3600)).toBe('4h');
  });

  it('floors fractional seconds', () => {
    expect(formatUptime(90.9)).toBe('1m');
  });
});
