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
}

public static class RadioBrowserApi
{
    private static readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(6) };
    private static readonly string[] Servers = { "https://de1.api.radio-browser.info", "https://nl1.api.radio-browser.info" };

    public static async Task<List<Station>> SearchStationsAsync(string query)
    {
        var results = new List<Station>();
        if (string.IsNullOrWhiteSpace(query)) return results;
        foreach (var server in Servers)
        {
            try
            {
                var json = await _client.GetStringAsync($"{server}/json/stations/search?name={Uri.EscapeDataString(query)}&limit=20&hidebroken=true");
                var items = JsonSerializer.Deserialize<List<RadioBrowserStation>>(json);
                if (items != null)
                {
                    foreach (var i in items)
                    {
                        if (!string.IsNullOrEmpty(i.url_resolved) && !string.IsNullOrEmpty(i.name))
                        {
                            results.Add(new Station(i.name.Trim(), i.url_resolved.Trim(), string.IsNullOrEmpty(i.tags)?i.country:i.tags.Split(',')[0], true));
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
