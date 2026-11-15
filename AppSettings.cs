using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace RADYJKO;

public class CustomEqPreset
{
    public string Name { get; set; } = string.Empty;
    public float[] Gains { get; set; } = new float[12];
}

public class HistoryEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Station { get; set; } = string.Empty;
    public string Track { get; set; } = string.Empty;

    public string DisplayText => $"{Timestamp:yyyy-MM-dd HH:mm} | [{Station}] {Track}";
}

public class UserSettings
{
    public float Volume { get; set; } = 0.75f;
    public string? LastStationUrl { get; set; }
    public bool AutostartEnabled { get; set; } = false;
    public bool ResumeOnStartup { get; set; } = false;
    public bool DarkTheme { get; set; } = true;
    public bool EnableCoverArt { get; set; } = true;
    public bool EnableDiscordRpc { get; set; } = true;
    public bool EnableAutoLeveler { get; set; } = true;
    public bool EnableNightCompressor { get; set; } = false;
    public bool EnableShoppingAutoSearch { get; set; } = true;
    public bool EnableWebRemote { get; set; } = true;
    public int WebRemotePort { get; set; } = 8080;
    public bool AlarmEnabled { get; set; } = false;
    public string AlarmTime { get; set; } = "07:00";
    public string AlarmStationUrl { get; set; } = string.Empty;
    public bool DvrAutoSplitTracks { get; set; } = true;
    public string Language { get; set; } = "PL";
    public float[] EqGains { get; set; } = new float[12];
    public List<CustomEqPreset> UserEqPresets { get; set; } = new();
    public List<ScheduledRecording> Schedules { get; set; } = new();
    public List<HistoryEntry> TrackHistory { get; set; } = new();
}

public static class SettingsManager
{
    private static readonly string AppFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RADYJKO");
    private static readonly string SettingsFile = Path.Combine(AppFolder, "settings_v50.json");

    public static UserSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var s = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(SettingsFile));
                if (s != null)
                {
                    if (s.EqGains == null || s.EqGains.Length != 12) s.EqGains = new float[12];
                    s.UserEqPresets ??= new();
                    s.Schedules ??= new();
                    s.TrackHistory ??= new();
                    return s;
                }
            }
        }
        catch { }
        return new UserSettings();
    }

    public static void Save(UserSettings settings)
    {
        try
        {
            Directory.CreateDirectory(AppFolder);
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
