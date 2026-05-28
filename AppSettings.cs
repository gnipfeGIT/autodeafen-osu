using System.Text.Json;

namespace AutoDeafenOsu;

public sealed class AppSettings
{
    public string GosumemoryEndpoint { get; set; } = "http://127.0.0.1:24050/json";
    public string DiscordToggleDeafenHotkey { get; set; } = "Ctrl+Shift+D";
    public HotkeySendMethod HotkeySendMethod { get; set; } = HotkeySendMethod.SendInputScanCode;
    public int PollIntervalMs { get; set; } = 500;
    public int MinComboToDeafen { get; set; } = 25;
    public bool UseMapMaxComboPercent { get; set; } = true;
    public int MaxComboPercentToDeafen { get; set; } = 75;
    public bool UndeafenOnMiss { get; set; } = true;
    public bool UndeafenWhenNotPlaying { get; set; } = true;
    public bool StartMonitoringOnLaunch { get; set; }
    public bool MinimizeToTray { get; set; } = true;

    public static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutoDeafenOsu");

    public static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions()) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        var json = JsonSerializer.Serialize(this, JsonOptions());
        File.WriteAllText(SettingsPath, json);
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
}
