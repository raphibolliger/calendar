using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Calendar.Processing;

public sealed class OutputWriter
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private readonly string assetDirectory;

    public OutputWriter(string assetDirectory)
    {
        this.assetDirectory = assetDirectory;
        Directory.CreateDirectory(assetDirectory);
    }

    public static string ResolveAssetDirectory()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Calendar.Frontend", "public", "data");
            if (Directory.Exists(Path.Combine(directory.FullName, "Calendar.Frontend")))
                return candidate;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException(
            "Verzeichnis 'Calendar.Frontend' wurde oberhalb des Arbeitsverzeichnisses nicht gefunden.");
    }

    public DataManifest Write(AvailabilityPayload availability, DetailsPayload details, string? detailsSecret)
    {
        foreach (var stale in Directory.EnumerateFiles(assetDirectory)
            .Where(path => Path.GetFileName(path) is var name
                && (name.StartsWith("availability-", StringComparison.Ordinal)
                    || name.StartsWith("details-", StringComparison.Ordinal)
                    || name == "data-manifest.json")))
        {
            File.Delete(stale);
        }

        var token = Guid.NewGuid().ToString("N");
        var availabilityFile = $"availability-{token}.json";
        File.WriteAllText(
            Path.Combine(assetDirectory, availabilityFile),
            JsonSerializer.Serialize(availability, WriteOptions));

        if (!string.IsNullOrWhiteSpace(detailsSecret))
        {
            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(detailsSecret)));
            File.WriteAllText(
                Path.Combine(assetDirectory, $"details-{token}-{hash}.json"),
                JsonSerializer.Serialize(details, WriteOptions));
        }

        var manifest = new DataManifest(availability.GeneratedAt, availabilityFile, $"details-{token}");
        File.WriteAllText(
            Path.Combine(assetDirectory, "data-manifest.json"),
            JsonSerializer.Serialize(manifest, WriteOptions));

        return manifest;
    }
}
