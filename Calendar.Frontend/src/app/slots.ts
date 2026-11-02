export interface Span {
  from: number;
  to: number;
}

export interface SlotSettings {
  slotMinutes: number;
  minSlotMinutes: number;
  bufferMinutes: number;
}

export function mergeSpans(spans: readonly Span[]): Span[] {
  const ordered = spans.filter((span) => span.to > span.from).sort((a, b) => a.from - b.from);
  const merged: Span[] = [];
  for (const span of ordered) {
    const last = merged[merged.length - 1];
    if (last && span.from <= last.to) {
      last.to = Math.max(last.to, span.to);
      continue;
    }
    merged.push({ ...span });
  }
  return merged;
}

export function subtractSpans(from: readonly Span[], cuts: readonly Span[]): Span[] {
  let remaining = from.map((span) => ({ ...span }));
  for (const cut of cuts) {
    const next: Span[] = [];
    for (const span of remaining) {
      if (cut.to <= span.from || cut.from >= span.to) {
        next.push(span);
        continue;
      }
      if (cut.from > span.from) {
        next.push({ from: span.from, to: cut.from });
      }
      if (cut.to < span.to) {
        next.push({ from: cut.to, to: span.to });
      }
    }
    remaining = next;
  }
  return remaining.filter((span) => span.to > span.from);
}

export function freeSpans(
  working: readonly Span[],
  blocked: readonly Span[],
  settings: SlotSettings,
  earliestMinute: number | null,
): Span[] {
  if (working.length === 0) {
    return [];
  }

  const cuts = mergeSpans(
    blocked.map((span) => ({
      from: span.from - settings.bufferMinutes,
      to: span.to + settings.bufferMinutes,
    })),
  );

  let free = subtractSpans(working, cuts);
  if (earliestMinute !== null) {
    free = free
      .map((span) => ({ from: Math.max(span.from, earliestMinute), to: span.to }))
      .filter((span) => span.to > span.from);
  }

  const grid = Math.max(settings.slotMinutes, 1);
  return free
    .map((span) => ({
      from: Math.ceil(span.from / grid) * grid,
      to: Math.floor(span.to / grid) * grid,
    }))
    .filter((span) => span.to - span.from >= settings.minSlotMinutes);
}
