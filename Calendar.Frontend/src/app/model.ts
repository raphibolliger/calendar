export interface DataManifest {
  generatedAt: string;
  availability: string;
  detailsPrefix: string;
}

export interface AvailabilityDefaults {
  workingHours: Record<string, string[]>;
  displayHours: string;
  slotMinutes: number;
  minSlotMinutes: number;
  leadTimeHours: number;
  bufferMinutes: number;
}

export interface BusyBlock {
  start: string;
  end: string;
  soft: boolean;
}

export interface Availability {
  generatedAt: string;
  timeZone: string;
  window: { from: string; to: string };
  defaults: AvailabilityDefaults;
  busy: BusyBlock[];
  awayDays: string[];
  markedDays: string[];
}

export interface DetailEntry {
  source: string;
  uid: string;
  title: string;
  start: string;
  end: string;
  allDay: boolean;
  recurring: boolean;
  recurrenceRule?: string;
  kind: 'Blocking' | 'Soft' | 'Away';
  reason: string;
}

export interface DetailsPayload {
  generatedAt: string;
  occurrences: DetailEntry[];
}
