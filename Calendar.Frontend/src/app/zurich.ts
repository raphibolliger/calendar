export const TIME_ZONE = 'Europe/Zurich';

const WALL_CLOCK = new Intl.DateTimeFormat('en-CA', {
  timeZone: TIME_ZONE,
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
});

const WEEKDAY_LONG = new Intl.DateTimeFormat('de-CH', { timeZone: 'UTC', weekday: 'long' });
const WEEKDAY_SHORT = new Intl.DateTimeFormat('de-CH', { timeZone: 'UTC', weekday: 'short' });
const DAY_MONTH = new Intl.DateTimeFormat('de-CH', { timeZone: 'UTC', day: 'numeric', month: 'long' });
const DAY_SHORT = new Intl.DateTimeFormat('de-CH', { timeZone: 'UTC', day: '2-digit', month: '2-digit' });

export interface WallClock {
  dayKey: string;
  minutes: number;
}

export function toWallClock(value: string | Date): WallClock {
  const date = typeof value === 'string' ? new Date(value) : value;
  const parts: Record<string, string> = {};
  for (const part of WALL_CLOCK.formatToParts(date)) {
    parts[part.type] = part.value;
  }
  return {
    dayKey: `${parts['year']}-${parts['month']}-${parts['day']}`,
    minutes: Number(parts['hour']) * 60 + Number(parts['minute']),
  };
}

function asDate(dayKey: string): Date {
  const [year, month, day] = dayKey.split('-').map(Number);
  return new Date(Date.UTC(year, month - 1, day));
}

export function addDays(dayKey: string, days: number): string {
  const date = asDate(dayKey);
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}

export function weekdayOf(dayKey: string): number {
  return asDate(dayKey).getUTCDay();
}

export function startOfWeek(dayKey: string): string {
  const weekday = weekdayOf(dayKey);
  return addDays(dayKey, weekday === 0 ? -6 : 1 - weekday);
}

export function daysBetween(from: string, to: string): number {
  return Math.round((asDate(to).getTime() - asDate(from).getTime()) / 86_400_000);
}

export function weekdayLong(dayKey: string): string {
  return WEEKDAY_LONG.format(asDate(dayKey));
}

export function weekdayShort(dayKey: string): string {
  return WEEKDAY_SHORT.format(asDate(dayKey)).replace('.', '');
}

export function dayAndMonth(dayKey: string): string {
  return DAY_MONTH.format(asDate(dayKey));
}

export function dayShort(dayKey: string): string {
  return DAY_SHORT.format(asDate(dayKey));
}

export function formatMinutes(minutes: number): string {
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  return `${String(hours).padStart(2, '0')}:${String(rest).padStart(2, '0')}`;
}

export function parseTimeRange(value: string): { from: number; to: number } {
  const [from, to] = value.split('-');
  return { from: parseMinutes(from), to: parseMinutes(to) };
}

export function parseMinutes(value: string): number {
  const [hours, minutes] = value.trim().split(':').map(Number);
  return hours * 60 + (minutes ?? 0);
}
