using System.Text;
using System.Text.RegularExpressions;

namespace Calendar.Processing;

public static partial class IcsSanitizer
{
    public static string Normalize(string ics)
    {
        var unfolded = FoldedLine().Replace(ics.Replace("\r\n", "\n"), string.Empty);
        var lines = unfolded.Split('\n');
        var result = new StringBuilder(unfolded.Length);
        var block = new List<string>();
        var inEvent = false;

        foreach (var line in lines)
        {
            if (line.StartsWith("BEGIN:VEVENT", StringComparison.Ordinal))
            {
                inEvent = true;
                block.Clear();
            }

            if (!inEvent)
            {
                result.Append(line).Append('\n');
                continue;
            }

            block.Add(line);

            if (!line.StartsWith("END:VEVENT", StringComparison.Ordinal))
                continue;

            inEvent = false;
            foreach (var fixedLine in AlignValueTypes(block))
                result.Append(fixedLine).Append('\n');
        }

        return result.ToString();
    }

    private static List<string> AlignValueTypes(List<string> block)
    {
        var startIndex = block.FindIndex(line => line.StartsWith("DTSTART", StringComparison.Ordinal));
        var endIndex = block.FindIndex(line => line.StartsWith("DTEND", StringComparison.Ordinal));
        if (startIndex < 0 || endIndex < 0)
            return block;

        var start = Property.Parse(block[startIndex]);
        var end = Property.Parse(block[endIndex]);
        if (start.IsDateOnly == end.IsDateOnly)
            return block;

        block[endIndex] = start.IsDateOnly
            ? $"DTEND;VALUE=DATE:{end.Value[..8]}"
            : $"DTEND{start.Parameters}:{end.Value[..8]}T000000{(start.Value.EndsWith('Z') ? "Z" : string.Empty)}";

        return block;
    }

    private readonly record struct Property(string Parameters, string Value)
    {
        public bool IsDateOnly =>
            Parameters.Contains("VALUE=DATE", StringComparison.OrdinalIgnoreCase)
            || (Value.Length == 8 && Value.All(char.IsAsciiDigit));

        public static Property Parse(string line)
        {
            var colon = line.IndexOf(':');
            var head = colon < 0 ? line : line[..colon];
            var value = colon < 0 ? string.Empty : line[(colon + 1)..];
            var semicolon = head.IndexOf(';');
            return new Property(semicolon < 0 ? string.Empty : head[semicolon..], value);
        }
    }

    [GeneratedRegex(@"\n[ \t]")]
    private static partial Regex FoldedLine();
}
