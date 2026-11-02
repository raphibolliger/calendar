using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;

namespace Calendar.Processing;

public sealed record PipelineSettings(
    AvailabilityConfig Availability,
    IReadOnlyList<CalendarSource> Sources,
    string? DetailsSecret);

public static class AppSettings
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static PipelineSettings Load()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var defaults = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "appsettings.json")))!["Availability"]!.AsObject();
        var overrides = configuration["AvailabilityOverrides"];
        if (!string.IsNullOrWhiteSpace(overrides))
            Merge(defaults, JsonNode.Parse(overrides)!.AsObject());

        var availability = defaults.Deserialize<AvailabilityConfig>(ReadOptions)
            ?? throw new InvalidOperationException("Verfügbarkeits-Konfiguration konnte nicht gelesen werden.");

        var sourcesJson = configuration["CalendarSources"];
        if (string.IsNullOrWhiteSpace(sourcesJson))
            throw new InvalidOperationException(
                "Keine Kalenderquellen konfiguriert. Erwartet wird ein JSON-Array in 'CalendarSources' "
                + "(lokal als User Secret, in CI als Umgebungsvariable).");

        var sources = JsonSerializer.Deserialize<List<CalendarSource>>(sourcesJson, ReadOptions)
            ?? throw new InvalidOperationException("'CalendarSources' ist kein gültiges JSON-Array.");
        if (sources.Count == 0)
            throw new InvalidOperationException("'CalendarSources' enthält keine Einträge.");

        return new PipelineSettings(availability, sources, configuration["DetailsSecret"]);
    }

    private static void Merge(JsonObject target, JsonObject source)
    {
        foreach (var (key, value) in source)
        {
            if (value is JsonObject nested && target[key] is JsonObject existing)
                Merge(existing, nested);
            else
                target[key] = value?.DeepClone();
        }
    }
}
