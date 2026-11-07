using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RADYJKO;

public class SpotifyService
{
    private string? _accessToken;
    private readonly HttpClient _http = new();
    private const string RedirectUri = "http://localhost:8888/callback/";
    public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken);

    public async Task<bool> AuthenticateAsync(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId)) return false;
        try
        {
            var authUrl = $"https://accounts.spotify.com/authorize?client_id={clientId}&response_type=token&redirect_uri={Uri.EscapeDataString(RedirectUri)}&scope=user-library-modify";
            Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });
            using var listener = new HttpListener();
            listener.Prefixes.Add(RedirectUri);
            listener.Start();
            var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            while (!cts.IsCancellationRequested)
            {
                var ctx = await listener.GetContextAsync();
                var req = ctx.Request; var res = ctx.Response;
                if (req.QueryString["token"] != null)
                {
                    _accessToken = req.QueryString["token"];
                    byte[] okBytes = Encoding.UTF8.GetBytes("<html><body style='background:#121212;color:#1DB954;text-align:center;padding:50px;'><h1>Zalogowano do Spotify!</h1><script>window.close();</script></body></html>");
                    res.OutputStream.Write(okBytes, 0, okBytes.Length); res.Close();
                    return true;
                }
                else
                {
                    string html = "<html><body><script>if(window.location.hash) { window.location.href = '?token=' + window.location.hash.substring(1).split('&')[0].split('=')[1]; }</script>Autoryzacja...</body></html>";
                    byte[] bytes = Encoding.UTF8.GetBytes(html);
                    res.OutputStream.Write(bytes, 0, bytes.Length); res.Close();
                }
            }
        }
        catch { }
        return false;
    }

    public async Task<string> SaveTrackAsync(string trackQuery)
    {
        if (!IsAuthenticated) return "Zaloguj się najpierw do Spotify.";
        if (string.IsNullOrWhiteSpace(trackQuery)) return "Brak utworu do zapisania.";
        try
        {
            var cleanQuery = trackQuery.Split('(')[0].Split('[')[0].Trim();
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            var searchUrl = $"https://api.spotify.com/v1/search?q={Uri.EscapeDataString(cleanQuery)}&type=track&limit=1";
            var searchRes = await _http.GetStringAsync(searchUrl);
            using var doc = JsonDocument.Parse(searchRes);
            var tracks = doc.RootElement.GetProperty("tracks").GetProperty("items");
            if (tracks.GetArrayLength() == 0) return "Nie znaleziono utworu na Spotify.";
            var trackId = tracks[0].GetProperty("id").GetString();
            var putRes = await _http.PutAsync($"https://api.spotify.com/v1/me/tracks?ids={trackId}", null);
            return putRes.IsSuccessStatusCode ? "Dodano do Polubionych! 💚" : "Błąd zapisu.";
        }
        catch (Exception ex) { return $"Błąd API: {ex.Message}"; }
    }
}
