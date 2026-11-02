namespace Calendar.Processing;

public static class Intervals
{
    public static List<Interval> Merge(IEnumerable<Interval> intervals)
    {
        var ordered = intervals.Where(i => !i.IsEmpty).OrderBy(i => i.Start).ToList();
        var merged = new List<Interval>();
        foreach (var interval in ordered)
        {
            if (merged.Count > 0 && interval.Start <= merged[^1].End)
            {
                if (interval.End > merged[^1].End)
                    merged[^1] = merged[^1] with { End = interval.End };
                continue;
            }
            merged.Add(interval);
        }
        return merged;
    }

    public static List<Interval> Subtract(IEnumerable<Interval> from, IReadOnlyList<Interval> remove)
    {
        var result = new List<Interval>();
        foreach (var interval in from)
        {
            var remaining = new List<Interval> { interval };
            foreach (var cut in remove)
            {
                var next = new List<Interval>();
                foreach (var part in remaining)
                {
                    if (cut.End <= part.Start || cut.Start >= part.End)
                    {
                        next.Add(part);
                        continue;
                    }
                    if (cut.Start > part.Start)
                        next.Add(part with { End = cut.Start });
                    if (cut.End < part.End)
                        next.Add(part with { Start = cut.End });
                }
                remaining = next;
            }
            result.AddRange(remaining.Where(i => !i.IsEmpty));
        }
        return result;
    }

    public static List<Interval> Intersect(IEnumerable<Interval> intervals, IReadOnlyList<Interval> windows)
    {
        var result = new List<Interval>();
        foreach (var interval in intervals)
        {
            foreach (var window in windows)
            {
                var start = interval.Start > window.Start ? interval.Start : window.Start;
                var end = interval.End < window.End ? interval.End : window.End;
                if (end > start)
                    result.Add(new Interval(start, end));
            }
        }
        return result.OrderBy(i => i.Start).ToList();
    }
}
