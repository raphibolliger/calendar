import { Span } from './slots';

export interface LabelledSpan extends Span {
  title: string;
  source: string;
  kind: 'Blocking' | 'Soft' | 'Away';
  reason: string;
}

export interface DayColumn {
  dayKey: string;
  weekdayShort: string;
  dayShort: string;
  isToday: boolean;
  isWorkday: boolean;
  away: boolean;
  marked: boolean;
  inWindow: boolean;
  hard: Span[];
  soft: Span[];
  slots: Span[];
  labelled: LabelledSpan[];
}
