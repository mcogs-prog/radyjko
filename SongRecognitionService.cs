using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using NAudio.Wave;

namespace RADYJKO;

public static class SongRecognitionService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static async Task<(string? Title, string? CoverUrl, string Message)> RecognizeFromLoopbackAsync()
    {
        try
        {
            // Capture 4 seconds of system WASAPI loopback audio to verify active signal & measure spectral energy
            float peakEnergy = 0f;
            int sampleRate = 44100;
            await Task.Run(async () =>
            {
                using var capture = new WasapiLoopbackCapture();
                sampleRate = capture.WaveFormat.SampleRate;
                capture.DataAvailable += (s, e) =>
                {
                    for (int i = 0; i < e.BytesRecorded - 3; i += 4)
                    {
                        float sample = Math.Abs(BitConverter.ToSingle(e.Buffer, i));
                        if (sample > peakEnergy) peakEnergy = sample;
                    }
                };
                capture.StartRecording();
                await Task.Delay(3500);
                capture.StopRecording();
            });

            if (peakEnergy < 0.005f)
            {
                return (null, null, "Brak wykrytego sygnału audio (włącz stację przed rozpoznawaniem).");
            }

            return (null, null, $"Przechwycono próbkę WASAPI ({sampleRate} Hz, szczyt {(int)(peakEnergy * 100)}%). Wpisz fragment tekstu lub wykonawcę w wyszukiwarce poniżej, jeśli stacja nie nadaje RDS.");
        }
        catch (Exception ex)
        {
            return (null, null, $"Błąd przechwytywania audio: {ex.Message}");
        }
    }

    public static async Task<(string? FullTitle, string? CoverUrl)> SearchTrackMetadataAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return (null, null);
        try
        {
            var url = $"https://itunes.apple.com/search?term={Uri.EscapeDataString(query.Trim())}&entity=song&limit=1";
            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
            {
                var item = results[0];
                string artist = item.TryGetProperty("artistName", out var a) ? a.GetString() ?? "" : "";
                string track = item.TryGetProperty("trackName", out var t) ? t.GetString() ?? "" : "";
                string? art = item.TryGetProperty("artworkUrl100", out var img) ? img.GetString()?.Replace("100x100bb.jpg", "512x512bb.jpg") : null;
                if (!string.IsNullOrEmpty(artist) && !string.IsNullOrEmpty(track))
                {
                    return ($"{artist} - {track}", art);
                }
            }
        }
        catch { }
        return (null, null);
    }
}
