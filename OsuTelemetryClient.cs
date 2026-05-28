using System.Globalization;
using System.Text.Json;

namespace AutoDeafenOsu;

public sealed class OsuTelemetryClient : IDisposable
{
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    public async Task<OsuTelemetrySnapshot> ReadAsync(string endpoint, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(endpoint, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        var combo = ReadInt(root, "gameplay.score.combo")
            ?? ReadInt(root, "gameplay.combo.current")
            ?? ReadInt(root, "gameplay.combo")
            ?? 0;

        var score = ReadInt(root, "gameplay.score") ?? 0;

        var misses = ReadInt(root, "gameplay.hits.0")
            ?? ReadInt(root, "gameplay.score.miss")
            ?? ReadInt(root, "gameplay.score.misses")
            ?? ReadInt(root, "gameplay.score.countMiss")
            ?? 0;

        var mapTime = ReadInt(root, "menu.bm.time.current") ?? 0;
        var fullTime = ReadInt(root, "menu.bm.time.full");

        var maxCombo = ReadInt(root, "menu.bm.stats.maxCombo")
            ?? ReadInt(root, "menu.bm.stats.fullCombo")
            ?? ReadInt(root, "menu.bm.stats.fc")
            ?? ReadInt(root, "menu.bm.maxCombo")
            ?? ReadInt(root, "menu.maxCombo");

        var stateNumber = ReadInt(root, "menu.state");
        var stateText = ReadString(root, "menu.state") ?? string.Empty;
        var isPlaying = stateNumber is 2 or 7 || stateText.Contains("play", StringComparison.OrdinalIgnoreCase);
        if (fullTime is > 0 && mapTime > fullTime + 2500)
        {
            isPlaying = false;
        }

        var title = BuildBeatmapTitle(root);
        return new OsuTelemetrySnapshot(true, isPlaying, combo, score, misses, maxCombo, mapTime, title, stateNumber, stateText);
    }

    public void Dispose() => _httpClient.Dispose();

    private static string BuildBeatmapTitle(JsonElement root)
    {
        var artist = ReadString(root, "menu.bm.metadata.artist");
        var title = ReadString(root, "menu.bm.metadata.title");
        var difficulty = ReadString(root, "menu.bm.metadata.difficulty")
            ?? ReadString(root, "menu.bm.metadata.version");

        if (!string.IsNullOrWhiteSpace(artist) && !string.IsNullOrWhiteSpace(title))
        {
            return string.IsNullOrWhiteSpace(difficulty)
                ? $"{artist} - {title}"
                : $"{artist} - {title} [{difficulty}]";
        }

        return ReadString(root, "menu.bm.path.full")
            ?? ReadString(root, "menu.bm.id")
            ?? "Unknown beatmap";
    }

    private static int? ReadInt(JsonElement root, string path)
    {
        if (!TryRead(root, path, out var element))
        {
            return null;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) => number,
            _ => null
        };
    }

    private static string? ReadString(JsonElement root, string path)
    {
        if (!TryRead(root, path, out var element))
        {
            return null;
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            _ => null
        };
    }

    private static bool TryRead(JsonElement root, string path, out JsonElement element)
    {
        element = root;
        foreach (var part in path.Split('.'))
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(part, out element))
            {
                return false;
            }
        }

        return true;
    }
}

public sealed record OsuTelemetrySnapshot(
    bool Available,
    bool IsPlaying,
    int Combo,
    int Score,
    int Misses,
    int? MaxCombo,
    int MapTimeMs,
    string BeatmapTitle,
    int? StateNumber,
    string StateText);
