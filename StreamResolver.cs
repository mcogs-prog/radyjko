using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace RADYJKO;

public static class StreamResolver
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public static async Task<string> ResolveUrlAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return url;
        try
        {
            if (url.EndsWith(".m3u", StringComparison.OrdinalIgnoreCase) || 
                url.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) || 
                url.EndsWith(".pls", StringComparison.OrdinalIgnoreCase) ||
                url.Contains(".m3u"))
            {
                var content = await _http.GetStringAsync(url);
                var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var plsFile = lines.FirstOrDefault(l => l.StartsWith("File1=", StringComparison.OrdinalIgnoreCase));
                if (plsFile != null) return plsFile.Substring(6).Trim();
                var m3uFile = lines.FirstOrDefault(l => !l.StartsWith("#") && l.StartsWith("http", StringComparison.OrdinalIgnoreCase));
                if (m3uFile != null) return m3uFile.Trim();
            }
        }
        catch { }
        return url;
    }
}
