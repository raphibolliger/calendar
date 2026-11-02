using Calendar.Processing;

var settings = AppSettings.Load();
var timeZone = TimeZoneInfo.FindSystemTimeZoneById(settings.Availability.TimeZone);
var clock = new LocalClock(timeZone);
var config = settings.Availability;

var today = DateOnly.FromDateTime(clock.Now.Date);
var windowStart = clock.StartOfDay(today);
var windowEnd = clock.StartOfDay(today.AddDays(config.HorizonWeeks * 7));

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
http.DefaultRequestHeaders.UserAgent.ParseAdd("calendar-availability-pipeline/1.0");
var loader = new FeedLoader(http);
var reader = new OccurrenceReader(config, clock);

var occurrences = new List<CalendarOccurrence>();
foreach (var source in settings.Sources)
{
    var calendar = await loader.LoadAsync(source, CancellationToken.None);
    var read = reader.Read(source, calendar, windowStart, windowEnd);
    occurrences.AddRange(read);
    Console.WriteLine(
        $"{source.Name}: {read.Count} Vorkommen im Fenster "
        + $"({read.Count(o => o.Kind == OccurrenceKind.Blocking)} blockierend, "
        + $"{read.Count(o => o.Kind == OccurrenceKind.Soft)} nicht blockierend, "
        + $"{read.Count(o => o.Kind == OccurrenceKind.Away)} abwesend)");
}

var displayHours = TimeRange.Parse(config.DisplayHours);
var displayWindows = new List<Interval>();
for (var day = today; day < today.AddDays(config.HorizonWeeks * 7); day = day.AddDays(1))
    displayWindows.Add(new Interval(clock.At(day, displayHours.From), clock.At(day, displayHours.To)));

var timed = occurrences.Where(o => !o.IsAllDay).ToList();
var hard = Intervals.Merge(timed
    .Where(o => o.Kind is OccurrenceKind.Blocking or OccurrenceKind.Away)
    .Select(o => new Interval(o.Start, o.End)));
var soft = Intervals.Subtract(
    Intervals.Merge(timed.Where(o => o.Kind == OccurrenceKind.Soft).Select(o => new Interval(o.Start, o.End))),
    hard);

var busy = Intervals.Intersect(hard, displayWindows)
    .Select(i => new BusyBlock(i.Start, i.End, false))
    .Concat(Intervals.Intersect(soft, displayWindows).Select(i => new BusyBlock(i.Start, i.End, true)))
    .OrderBy(block => block.Start)
    .ToList();

var awayDays = DaysOf(occurrences.Where(o => o.IsAllDay && o.Kind == OccurrenceKind.Away));
var markedDays = DaysOf(occurrences.Where(o => o.IsAllDay && o.Kind == OccurrenceKind.Soft))
    .Except(awayDays)
    .ToList();

var availability = new AvailabilityPayload(
    clock.Now,
    config.TimeZone,
    new WindowInfo(today, today.AddDays(config.HorizonWeeks * 7).AddDays(-1)),
    new DefaultsInfo(
        config.WorkingHours,
        config.DisplayHours,
        config.SlotMinutes,
        config.MinSlotMinutes,
        config.LeadTimeHours,
        config.BufferMinutes),
    busy,
    awayDays,
    markedDays);

var details = new DetailsPayload(
    clock.Now,
    occurrences
        .OrderBy(o => o.Start)
        .Select(o => new DetailEntry(
            o.SourceName,
            o.Uid,
            o.Title,
            o.Start,
            o.End,
            o.IsAllDay,
            o.IsRecurring,
            o.RecurrenceRule,
            o.Kind.ToString(),
            o.Reason))
        .ToList());

var writer = new OutputWriter(OutputWriter.ResolveAssetDirectory());
var manifest = writer.Write(availability, details, settings.DetailsSecret);

Console.WriteLine(
    $"Geschrieben: {manifest.Availability} – {busy.Count} Blöcke, "
    + $"{awayDays.Count} Abwesenheitstage, {markedDays.Count} markierte Tage"
    + (string.IsNullOrWhiteSpace(settings.DetailsSecret) ? " (ohne Detaildatei, kein DetailsSecret gesetzt)" : string.Empty));

List<DateOnly> DaysOf(IEnumerable<CalendarOccurrence> source)
{
    var days = new SortedSet<DateOnly>();
    var limit = today.AddDays(config.HorizonWeeks * 7);
    foreach (var occurrence in source)
    {
        for (var day = occurrence.FirstDay; day <= occurrence.LastDay; day = day.AddDays(1))
        {
            if (day >= today && day < limit)
                days.Add(day);
        }
    }
    return [.. days];
}
