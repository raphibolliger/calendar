using IcsCalendar = Ical.Net.Calendar;

namespace Calendar.Processing;

public sealed class FeedLoader(HttpClient http)
{
    public async Task<IcsCalendar> LoadAsync(CalendarSource source, CancellationToken cancellationToken)
    {
        var url = source.Url
            .Replace("webcal://", "https://", StringComparison.OrdinalIgnoreCase)
            .Replace("webcals://", "https://", StringComparison.OrdinalIgnoreCase);

        var ics = await http.GetStringAsync(url, cancellationToken);
        if (!ics.Contains("BEGIN:VCALENDAR", StringComparison.Ordinal))
            throw new InvalidOperationException($"Antwort von '{source.Name}' ist kein iCalendar-Feed.");

        return IcsCalendar.Load(IcsSanitizer.Normalize(ics))
            ?? throw new InvalidOperationException($"Feed '{source.Name}' konnte nicht gelesen werden.");
    }
}
