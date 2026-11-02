import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { AvailabilityStore } from './availability-store';
import { Span, freeSpans } from './slots';
import { SlotList } from './slot-list';
import { DayColumn, LabelledSpan } from './view-model';
import { WeekGrid } from './week-grid';
import {
  addDays,
  dayShort,
  daysBetween,
  formatMinutes,
  parseMinutes,
  parseTimeRange,
  startOfWeek,
  toWallClock,
  weekdayOf,
  weekdayShort,
} from './zurich';

const WEEKDAY_KEYS = [
  'Sunday',
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
] as const;

const WEEKDAY_ORDER = [1, 2, 3, 4, 5, 6, 0];

@Component({
  selector: 'app-root',
  imports: [WeekGrid, SlotList],
  templateUrl: './app.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  private readonly store = inject(AvailabilityStore);

  readonly availability = this.store.availability;
  readonly detailsUnlocked = this.store.detailsUnlocked;

  readonly todayKey = toWallClock(new Date()).dayKey;
  readonly weekStart = signal(startOfWeek(this.todayKey));

  readonly workFrom = signal(8 * 60);
  readonly workTo = signal(18 * 60);
  readonly activeWeekdays = signal<number[]>([1, 2, 3, 4, 5]);
  readonly minSlot = signal(60);
  readonly buffer = signal(15);
  readonly grid = signal(30);
  readonly leadHours = signal(24);
  readonly listSpanDays = signal(14);
  readonly selectedSeries = signal<string[]>([]);

  readonly weekdayButtons = WEEKDAY_ORDER.map((index) => ({
    index,
    label: weekdayShort(addDays('2026-11-01', index)),
  }));

  private readonly nowTick = signal(Date.now());
  private initialised = false;

  constructor() {
    effect(() => {
      const defaults = this.availability.value()?.defaults;
      if (!defaults || this.initialised) {
        return;
      }
      this.initialised = true;

      const active: number[] = [];
      let from = 24 * 60;
      let to = 0;
      WEEKDAY_KEYS.forEach((key, index) => {
        const ranges = defaults.workingHours[key] ?? [];
        if (ranges.length === 0) {
          return;
        }
        active.push(index);
        for (const range of ranges) {
          const parsed = parseTimeRange(range);
          from = Math.min(from, parsed.from);
          to = Math.max(to, parsed.to);
        }
      });

      if (active.length > 0) {
        this.activeWeekdays.set(active);
        this.workFrom.set(from);
        this.workTo.set(to);
      }
      this.minSlot.set(defaults.minSlotMinutes);
      this.buffer.set(defaults.bufferMinutes);
      this.grid.set(defaults.slotMinutes);
      this.leadHours.set(defaults.leadTimeHours);
    });

    window.setInterval(() => this.nowTick.set(Date.now()), 60_000);
  }

  readonly display = computed<Span>(() => {
    const configured = this.availability.value()?.defaults.displayHours ?? '06:00-23:00';
    const parsed = parseTimeRange(configured);
    return { from: Math.min(parsed.from, this.workFrom()), to: Math.max(parsed.to, this.workTo()) };
  });

  readonly weekColumns = computed(() =>
    Array.from({ length: 7 }, (_, offset) => this.column(addDays(this.weekStart(), offset))),
  );

  readonly listColumns = computed(() => {
    const span = this.listSpanDays();
    if (span === -1) {
      return this.weekColumns();
    }
    const windowEnd = this.availability.value()?.window.to ?? this.todayKey;
    const end = span === 0 ? windowEnd : addDays(this.todayKey, span - 1);
    const count = Math.max(daysBetween(this.todayKey, end) + 1, 0);
    return Array.from({ length: count }, (_, offset) => this.column(addDays(this.todayKey, offset)));
  });

  readonly weekLabel = computed(() => {
    const start = this.weekStart();
    return `${dayShort(start)} – ${dayShort(addDays(start, 6))}`;
  });

  readonly series = computed(() => {
    const payload = this.store.details.value();
    if (!payload) {
      return [];
    }
    const map = new Map<string, { uid: string; title: string; source: string; count: number; rule: string; kind: string }>();
    for (const entry of payload.occurrences) {
      if (!entry.recurring) {
        continue;
      }
      const existing = map.get(entry.uid);
      if (existing) {
        existing.count += 1;
        continue;
      }
      map.set(entry.uid, {
        uid: entry.uid,
        title: entry.title,
        source: entry.source,
        count: 1,
        rule: entry.recurrenceRule ?? '',
        kind: entry.kind,
      });
    }
    return [...map.values()].sort((left, right) => right.count - left.count);
  });

  readonly seriesSnippet = computed(() =>
    JSON.stringify({ NonBlockingSeriesUids: this.selectedSeries() }, null, 2),
  );

  readonly generatedAt = computed(() => {
    const value = this.availability.value()?.generatedAt;
    if (!value) {
      return '';
    }
    return new Intl.DateTimeFormat('de-CH', {
      timeZone: 'Europe/Zurich',
      dateStyle: 'short',
      timeStyle: 'short',
    }).format(new Date(value));
  });

  shiftWeek(weeks: number): void {
    this.weekStart.set(addDays(this.weekStart(), weeks * 7));
  }

  resetWeek(): void {
    this.weekStart.set(startOfWeek(this.todayKey));
  }

  toggleWeekday(index: number): void {
    const active = this.activeWeekdays();
    this.activeWeekdays.set(
      active.includes(index) ? active.filter((value) => value !== index) : [...active, index],
    );
  }

  toggleSeries(uid: string): void {
    const selected = this.selectedSeries();
    this.selectedSeries.set(
      selected.includes(uid) ? selected.filter((value) => value !== uid) : [...selected, uid],
    );
  }

  setWorkFrom(value: string): void {
    this.workFrom.set(parseMinutes(value));
  }

  setWorkTo(value: string): void {
    this.workTo.set(parseMinutes(value));
  }

  asTime(minutes: number): string {
    return formatMinutes(minutes);
  }

  reload(): void {
    this.store.reload();
  }

  async copySnippet(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.seriesSnippet());
    } catch {
      return;
    }
  }

  private readonly busyByDay = computed(() => {
    const map = new Map<string, { hard: Span[]; soft: Span[] }>();
    const data = this.availability.value();
    if (!data) {
      return map;
    }
    for (const block of data.busy) {
      const start = toWallClock(block.start);
      const end = toWallClock(block.end);
      const entry = map.get(start.dayKey) ?? { hard: [], soft: [] };
      const span = { from: start.minutes, to: end.dayKey === start.dayKey ? end.minutes : 24 * 60 };
      (block.soft ? entry.soft : entry.hard).push(span);
      map.set(start.dayKey, entry);
    }
    return map;
  });

  private readonly labelledByDay = computed(() => {
    const map = new Map<string, LabelledSpan[]>();
    const payload = this.store.details.value();
    if (!payload) {
      return map;
    }
    for (const entry of payload.occurrences) {
      if (entry.allDay) {
        continue;
      }
      const start = toWallClock(entry.start);
      const end = toWallClock(entry.end);
      const list = map.get(start.dayKey) ?? [];
      list.push({
        from: start.minutes,
        to: end.dayKey === start.dayKey ? end.minutes : 24 * 60,
        title: entry.title,
        source: entry.source,
        kind: entry.kind,
        reason: entry.reason,
      });
      map.set(start.dayKey, list);
    }
    return map;
  });

  private readonly cutoff = computed(() => toWallClock(new Date(this.nowTick() + this.leadHours() * 3_600_000)));

  private column(dayKey: string): DayColumn {
    const data = this.availability.value();
    const busy = this.busyByDay().get(dayKey) ?? { hard: [], soft: [] };
    const weekday = weekdayOf(dayKey);
    const isWorkday = this.activeWeekdays().includes(weekday);
    const inWindow = data !== undefined && dayKey >= data.window.from && dayKey <= data.window.to;
    const away = data !== undefined && data.awayDays.includes(dayKey);
    const marked = data !== undefined && data.markedDays.includes(dayKey);
    const cutoff = this.cutoff();

    const working: Span[] =
      isWorkday && inWindow && !away ? [{ from: this.workFrom(), to: this.workTo() }] : [];
    const slots =
      dayKey < cutoff.dayKey
        ? []
        : freeSpans(
            working,
            busy.hard,
            {
              slotMinutes: this.grid(),
              minSlotMinutes: this.minSlot(),
              bufferMinutes: this.buffer(),
            },
            dayKey === cutoff.dayKey ? cutoff.minutes : null,
          );

    return {
      dayKey,
      weekdayShort: weekdayShort(dayKey),
      dayShort: dayShort(dayKey),
      isToday: dayKey === this.todayKey,
      isWorkday,
      away,
      marked,
      inWindow,
      hard: busy.hard,
      soft: busy.soft,
      slots,
      labelled: this.labelledByDay().get(dayKey) ?? [],
    };
  }
}
