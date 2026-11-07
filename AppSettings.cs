using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace RADYJKO;

public class UserSettings
{
    public float Volume { get; set; } = 0.75f;
    public string? LastStationUrl { get; set; }
    public bool AutostartEnabled { get; set; } = false;
    public bool ResumeOnStartup { get; set; } = false;
    public bool EnableCoverArt { get; set; } = true;
    public bool EnableDiscordRpc { get; set; } = true;
    public bool EnableAutoLeveler { get; set; } = true;
    public bool EnableWebRemote { get; set; } = true;
    public int WebRemotePort { get; set; } = 8080;
    public string SpotifyClientId { get; set; } = string.Empty;
    public string Language { get; set; } = "PL"; // PL, EN, LT, DE
    public float[] EqGains { get; set; } = new float[12];
    public List<ScheduledRecording> Schedules { get; set; } = new();
}

public static class SettingsManager
{
    private static readonly string AppFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RADYJKO");
    private static readonly string SettingsFile = Path.Combine(AppFolder, "settings_v33.json");

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
