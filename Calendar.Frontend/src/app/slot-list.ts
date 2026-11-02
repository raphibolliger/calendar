import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { DayColumn } from './view-model';
import { formatMinutes } from './zurich';

@Component({
  selector: 'app-slot-list',
  imports: [DecimalPipe],
  templateUrl: './slot-list.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SlotList {
  readonly days = input.required<DayColumn[]>();

  readonly copied = signal(false);

  readonly lines = computed(() =>
    this.days()
      .filter((day) => day.slots.length > 0)
      .map((day) => ({
        dayKey: day.dayKey,
        text: `${day.weekdayShort} ${day.dayShort}: ${day.slots
          .map((slot) => `${formatMinutes(slot.from)}–${formatMinutes(slot.to)}`)
          .join(', ')}`,
      })),
  );

  readonly text = computed(() => this.lines().map((line) => line.text).join('\n'));

  readonly totalHours = computed(
    () =>
      this.days().reduce(
        (sum, day) => sum + day.slots.reduce((daySum, slot) => daySum + (slot.to - slot.from), 0),
        0,
      ) / 60,
  );

  async copy(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.text());
      this.copied.set(true);
      window.setTimeout(() => this.copied.set(false), 2000);
    } catch {
      this.copied.set(false);
    }
  }
}
