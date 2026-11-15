using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace RADYJKO;

public class RadioBrowserStation
{
    public string name { get; set; } = string.Empty;
    public string url_resolved { get; set; } = string.Empty;
    public string tags { get; set; } = string.Empty;
    public string country { get; set; } = string.Empty;
    public string codec { get; set; } = string.Empty;
    public int bitrate { get; set; }
    public int lastcheckok { get; set; }
}

public static class RadioBrowserApi
{
    private static readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(7) };
    private static readonly string[] Servers = { "https://de1.api.radio-browser.info", "https://nl1.api.radio-browser.info" };

    public static async Task<List<Station>> SearchStationsAsync(string query, string countryCode = "", string tag = "", int minBitrate = 0)
    {
        var results = new List<Station>();
        foreach (var server in Servers)
        {
            try
            {
                var url = $"{server}/json/stations/search?limit=35&hidebroken=true&order=votes&reverse=true";
                if (!string.IsNullOrWhiteSpace(query)) url += $"&name={Uri.EscapeDataString(query.Trim())}";
                if (!string.IsNullOrWhiteSpace(countryCode)) url += $"&countrycode={Uri.EscapeDataString(countryCode.Trim())}";
                if (!string.IsNullOrWhiteSpace(tag)) url += $"&tag={Uri.EscapeDataString(tag.Trim())}";
                if (minBitrate > 0) url += $"&bitrateMin={minBitrate}";

                var json = await _client.GetStringAsync(url);
                var items = JsonSerializer.Deserialize<List<RadioBrowserStation>>(json);
                if (items != null)
                {
                    foreach (var i in items)
                    {
                        if (!string.IsNullOrEmpty(i.url_resolved) && !string.IsNullOrEmpty(i.name))
                        {
                            string baseGenre = string.IsNullOrEmpty(i.tags) ? i.country : i.tags.Split(',')[0];
                            string techInfo = i.bitrate > 0 ? $" • {i.codec} {i.bitrate}k" : (!string.IsNullOrEmpty(i.codec) ? $" • {i.codec}" : "");
                            string statusDot = i.lastcheckok == 1 ? "🟢 " : "🟡 ";
                            results.Add(new Station($"{statusDot}{i.name.Trim()}", i.url_resolved.Trim(), $"{baseGenre}{techInfo}", true));
                        }
                    }
                    return results;
                }
            }
            catch { }
        }
        return results;
    }
}
