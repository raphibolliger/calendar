namespace Calendar.Processing;

public sealed class LocalClock(TimeZoneInfo timeZone)
{
    public TimeZoneInfo TimeZone { get; } = timeZone;

    public DateTimeOffset At(DateOnly day, TimeOnly time)
    {
        var naive = DateTime.SpecifyKind(day.ToDateTime(time), DateTimeKind.Unspecified);
        while (TimeZone.IsInvalidTime(naive))
            naive = naive.AddMinutes(15);
        var utc = TimeZoneInfo.ConvertTimeToUtc(naive, TimeZone);
        return TimeZoneInfo.ConvertTime(new DateTimeOffset(utc, TimeSpan.Zero), TimeZone);
    }

    public DateTimeOffset StartOfDay(DateOnly day) => At(day, TimeOnly.MinValue);

    public DateTimeOffset FromUtc(DateTime utc) =>
        TimeZoneInfo.ConvertTime(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)), TimeZone);

    public DateTimeOffset Now => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZone);
}
