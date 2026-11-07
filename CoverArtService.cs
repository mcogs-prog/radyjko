using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace RADYJKO;

public static class CoverArtService
{
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    public static async Task<string?> FetchCoverArtUrlAsync(string trackQuery)
    {
        if (string.IsNullOrWhiteSpace(trackQuery)) return null;
        try
        {
            var cleanQuery = trackQuery.Split('(')[0].Split('[')[0].Trim();
            var url = $"https://itunes.apple.com/search?term={Uri.EscapeDataString(cleanQuery)}&entity=song&limit=1";
            var response = await _httpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(response);
            if (doc.RootElement.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
            {
                var first = results[0];
                if (first.TryGetProperty("artworkUrl100", out var artUrlProp))
                {
                    var artUrl = artUrlProp.GetString();
                    if (!string.IsNullOrEmpty(artUrl)) return artUrl.Replace("100x100bb.jpg", "512x512bb.jpg");
                }
            }
        }
        catch { }
        return null;
    }
}
