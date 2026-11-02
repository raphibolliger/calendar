namespace Calendar.Processing;

public enum OccurrenceKind
{
    Ignored,
    Soft,
    Blocking,
    Away
}

public sealed record CalendarSource
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Url { get; init; }
}

public sealed record TimeRange(TimeOnly From, TimeOnly To)
{
    public static TimeRange Parse(string value)
    {
        var parts = value.Split('-', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            throw new FormatException($"Zeitbereich '{value}' erwartet im Format 'HH:mm-HH:mm'.");
        return new TimeRange(TimeOnly.Parse(parts[0]), TimeOnly.Parse(parts[1]));
    }
}

public sealed class AvailabilityConfig
{
    public string TimeZone { get; init; } = "Europe/Zurich";
    public int HorizonWeeks { get; init; } = 8;
    public int SlotMinutes { get; init; } = 30;
    public int MinSlotMinutes { get; init; } = 60;
    public int LeadTimeHours { get; init; } = 24;
    public int BufferMinutes { get; init; } = 15;
    public string DisplayHours { get; init; } = "06:00-23:00";
    public Dictionary<string, List<string>> WorkingHours { get; init; } = [];
    public List<string> AwayKeywords { get; init; } = [];
    public List<string> NonBlockingSeriesUids { get; init; } = [];
    public List<string> NonBlockingTitlePatterns { get; init; } = [];
}

public sealed record CalendarOccurrence
{
    public required string SourceId { get; init; }
    public required string SourceName { get; init; }
    public required string Uid { get; init; }
    public required string Title { get; init; }
    public required DateTimeOffset Start { get; init; }
    public required DateTimeOffset End { get; init; }
    public required bool IsAllDay { get; init; }
    public required DateOnly FirstDay { get; init; }
    public required DateOnly LastDay { get; init; }
    public required bool IsRecurring { get; init; }
    public string? RecurrenceRule { get; init; }
    public required OccurrenceKind Kind { get; init; }
    public required string Reason { get; init; }
}

public readonly record struct Interval(DateTimeOffset Start, DateTimeOffset End)
{
    public bool IsEmpty => End <= Start;
}
