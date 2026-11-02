namespace Calendar.Processing;

public sealed record WindowInfo(DateOnly From, DateOnly To);

public sealed record DefaultsInfo(
    Dictionary<string, List<string>> WorkingHours,
    string DisplayHours,
    int SlotMinutes,
    int MinSlotMinutes,
    int LeadTimeHours,
    int BufferMinutes);

public sealed record BusyBlock(DateTimeOffset Start, DateTimeOffset End, bool Soft);

public sealed record AvailabilityPayload(
    DateTimeOffset GeneratedAt,
    string TimeZone,
    WindowInfo Window,
    DefaultsInfo Defaults,
    IReadOnlyList<BusyBlock> Busy,
    IReadOnlyList<DateOnly> AwayDays,
    IReadOnlyList<DateOnly> MarkedDays);

public sealed record DetailEntry(
    string Source,
    string Uid,
    string Title,
    DateTimeOffset Start,
    DateTimeOffset End,
    bool AllDay,
    bool Recurring,
    string? RecurrenceRule,
    string Kind,
    string Reason);

public sealed record DetailsPayload(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<DetailEntry> Occurrences);

public sealed record DataManifest(
    DateTimeOffset GeneratedAt,
    string Availability,
    string DetailsPrefix);
