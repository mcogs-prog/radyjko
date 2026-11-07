using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace RADYJKO;

public static class StationStorage
{
    private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RADYJKO");
    private static readonly string FilePath = Path.Combine(Folder, "favorites.json");

    public static List<Station> LoadFavorites()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var l = JsonSerializer.Deserialize<List<Station>>(File.ReadAllText(FilePath));
                if (l != null && l.Count > 0) return l;
            }
        }
        catch { }
        return GetDefaults();
    }

    public static void SaveFavorites(IEnumerable<Station> stations)
    {
        try { Directory.CreateDirectory(Folder); File.WriteAllText(FilePath, JsonSerializer.Serialize(stations, new JsonSerializerOptions { WriteIndented = true })); } catch { }
    }

    public static List<Station> GetDefaults() => new()
    {
        new("Radio Nowy Świat", "https://stream.rcs.revma.com/ypqt40u0x1zuv", "Publicystyka"),
        new("Radio 357", "https://stream.rcs.revma.com/ye5kghkgcm0uv", "Publicystyka"),
        new("RMF FM", "https://rs6-krk2.rmfstream.pl/rmf_fm", "Pop"),
        new("Radio ZET", "https://zt01.cdn.eurozet.pl/zet-net.mp3", "Pop"),
        new("Czwórka Polskie Radio", "https://stream11.polskieradio.pl/pr4/_definst_/pr4.stream/playlist.m3u8", "Alternatywa"),
        new("Antyradio", "https://an01.cdn.eurozet.pl/ant-waw.mp3", "Rock")
    };
}
