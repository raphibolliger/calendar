using System.Text.RegularExpressions;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Evaluation;
using IcsCalendar = Ical.Net.Calendar;

namespace Calendar.Processing;

public sealed class OccurrenceReader(AvailabilityConfig config, LocalClock clock)
{
    private const int LookbackDays = 120;

    private readonly HashSet<string> nonBlockingUids = new(config.NonBlockingSeriesUids, StringComparer.OrdinalIgnoreCase);

    private readonly List<Regex> nonBlockingTitles = config.NonBlockingTitlePatterns
        .Select(pattern => new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        .ToList();

    public List<CalendarOccurrence> Read(
        CalendarSource source,
        IcsCalendar calendar,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd)
    {
        var searchFrom = new CalDateTime(windowStart.AddDays(-LookbackDays).UtcDateTime, "UTC", true);
        var events = calendar.Events.ToList();
        var overrides = events
            .Where(candidate => candidate.RecurrenceIdentifier is not null)
            .ToDictionary(
                candidate => OverrideKey(candidate.Uid, candidate.RecurrenceIdentifier!.StartTime),
                candidate => candidate);

        var result = new List<CalendarOccurrence>();

        foreach (var calendarEvent in events)
        {
            var (kind, reason) = Classify(calendarEvent);
            if (kind == OccurrenceKind.Ignored)
                continue;

            foreach (var occurrence in Expand(calendarEvent, searchFrom, windowEnd))
            {
                if (calendarEvent.RecurrenceIdentifier is null
                    && overrides.ContainsKey(OverrideKey(calendarEvent.Uid, occurrence.Period.StartTime)))
                {
                    continue;
                }

                var start = ToOffset(occurrence.Period.StartTime);
                var end = EndOf(occurrence);
                if (end <= windowStart)
                    continue;

                var isAllDay = !occurrence.Period.StartTime.HasTime;
                var firstDay = DateOnly.FromDateTime(start.Date);
                var lastDay = DateOnly.FromDateTime(end.AddTicks(-1).Date);
                var effectiveKind = kind;
                var effectiveReason = reason;
                if (kind == OccurrenceKind.Blocking && isAllDay)
                    effectiveKind = AllDayKind(calendarEvent, out effectiveReason);

                result.Add(new CalendarOccurrence
                {
                    SourceId = source.Id,
                    SourceName = source.Name,
                    Uid = calendarEvent.Uid ?? string.Empty,
                    Title = calendarEvent.Summary ?? string.Empty,
                    Start = start,
                    End = end,
                    IsAllDay = isAllDay,
                    FirstDay = firstDay,
                    LastDay = lastDay < firstDay ? firstDay : lastDay,
                    IsRecurring = calendarEvent.RecurrenceRule is not null,
                    RecurrenceRule = calendarEvent.RecurrenceRule?.ToString(),
                    Kind = effectiveKind,
                    Reason = effectiveReason
                });
            }
        }

        return result;
    }

    private List<Occurrence> Expand(CalendarEvent calendarEvent, CalDateTime searchFrom, DateTimeOffset windowEnd)
    {
        try
        {
            return calendarEvent
                .GetOccurrences(searchFrom, new EvaluationOptions())
                .TakeWhile(occurrence => ToOffset(occurrence.Period.StartTime) < windowEnd)
                .ToList();
        }
        catch (Exception exception)
        {
            Console.WriteLine($"  Übersprungen: '{calendarEvent.Summary}' – {exception.Message}");
            return [];
        }
    }

    private OccurrenceKind AllDayKind(CalendarEvent calendarEvent, out string reason)
    {
        var keyword = config.AwayKeywords.FirstOrDefault(word =>
            (calendarEvent.Summary ?? string.Empty).Contains(word, StringComparison.OrdinalIgnoreCase));

        if (keyword is not null)
        {
            reason = $"Ganztagestermin mit Stichwort '{keyword}'";
            return OccurrenceKind.Away;
        }

        reason = "Ganztagestermin ohne Abwesenheits-Stichwort";
        return OccurrenceKind.Soft;
    }

    private string OverrideKey(string? uid, CalDateTime? recurrenceId) =>
        $"{uid}|{(recurrenceId is null ? string.Empty : ToOffset(recurrenceId).ToUnixTimeSeconds().ToString())}";

    private (OccurrenceKind Kind, string Reason) Classify(CalendarEvent calendarEvent)
    {
        if (string.Equals(calendarEvent.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase))
            return (OccurrenceKind.Ignored, "Termin abgesagt");

        var title = calendarEvent.Summary ?? string.Empty;

        if (!string.IsNullOrEmpty(calendarEvent.Uid) && nonBlockingUids.Contains(calendarEvent.Uid))
            return (OccurrenceKind.Soft, "Serien-UID als nicht blockierend markiert");

        var matchedPattern = nonBlockingTitles.FirstOrDefault(regex => regex.IsMatch(title));
        if (matchedPattern is not null)
            return (OccurrenceKind.Soft, $"Namensmuster '{matchedPattern}' als nicht blockierend markiert");

        if (string.Equals(calendarEvent.Transparency, "TRANSPARENT", StringComparison.OrdinalIgnoreCase))
            return (OccurrenceKind.Soft, "Im Kalender als frei markiert");

        var busyStatus = PropertyValue(calendarEvent, "X-MICROSOFT-CDO-BUSYSTATUS");
        if (string.Equals(busyStatus, "FREE", StringComparison.OrdinalIgnoreCase))
            return (OccurrenceKind.Soft, "Im Kalender als frei markiert");

        var keyword = config.AwayKeywords.FirstOrDefault(word =>
            title.Contains(word, StringComparison.OrdinalIgnoreCase));

        if (keyword is not null || string.Equals(busyStatus, "OOF", StringComparison.OrdinalIgnoreCase))
            return (OccurrenceKind.Blocking, keyword is not null ? $"Stichwort '{keyword}'" : "Als abwesend markiert");

        return (OccurrenceKind.Blocking, "Belegt");
    }

    private static string? PropertyValue(CalendarEvent calendarEvent, string name) =>
        calendarEvent.Properties.ContainsKey(name) ? calendarEvent.Properties.Get<string>(name) : null;

    private DateTimeOffset EndOf(Occurrence occurrence)
    {
        var end = occurrence.Period.EffectiveEndTime;
        if (end is not null)
            return ToOffset(end);

        var start = ToOffset(occurrence.Period.StartTime);
        return occurrence.Period.StartTime.HasTime ? start.AddHours(1) : start.AddDays(1);
    }

    private DateTimeOffset ToOffset(CalDateTime value)
    {
        if (!value.HasTime)
            return clock.StartOfDay(value.Date);
        if (value.IsFloating)
            return clock.At(value.Date, value.Time ?? TimeOnly.MinValue);
        return clock.FromUtc(value.AsUtc);
    }
}
