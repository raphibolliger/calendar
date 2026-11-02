import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Span } from './slots';
import { DayColumn } from './view-model';
import { formatMinutes } from './zurich';

@Component({
  selector: 'app-week-grid',
  templateUrl: './week-grid.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WeekGrid {
  readonly columns = input.required<DayColumn[]>();
  readonly display = input.required<Span>();
  readonly showDetails = input(false);

  readonly pixelsPerHour = 46;

  readonly height = computed(() => ((this.display().to - this.display().from) / 60) * this.pixelsPerHour);

  readonly hourMarks = computed(() => {
    const { from, to } = this.display();
    const marks: { minute: number; label: string; offset: number }[] = [];
    for (let minute = Math.ceil(from / 60) * 60; minute <= to; minute += 60) {
      marks.push({ minute, label: formatMinutes(minute), offset: this.offsetOf(minute) });
    }
    return marks;
  });

  offsetOf(minute: number): number {
    return ((minute - this.display().from) / 60) * this.pixelsPerHour;
  }

  sizeOf(span: Span): number {
    const clamped = this.clamp(span);
    return Math.max(((clamped.to - clamped.from) / 60) * this.pixelsPerHour, 3);
  }

  topOf(span: Span): number {
    return this.offsetOf(this.clamp(span).from);
  }

  label(span: Span): string {
    return `${formatMinutes(span.from)}–${formatMinutes(span.to)}`;
  }

  private clamp(span: Span): Span {
    const { from, to } = this.display();
    return { from: Math.max(span.from, from), to: Math.min(span.to, to) };
  }
}
