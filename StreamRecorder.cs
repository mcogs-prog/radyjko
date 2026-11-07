using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RADYJKO;

public class StreamRecorder : IDisposable
{
    private CancellationTokenSource? _cts;
    private static readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly string RecordFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "RADYJKO");

    public bool IsRecording { get; private set; }
    public string? CurrentFilePath { get; private set; }
    public event Action<bool, string>? RecordingStateChanged;

    public void StartRecording(string streamUrl, string stationName, string? currentTrack)
    {
        StopRecording();
        Directory.CreateDirectory(RecordFolder);
        var baseName = !string.IsNullOrWhiteSpace(currentTrack) ? $"{stationName} - {currentTrack}" : $"{stationName}_{DateTime.Now:yyyyMMdd_HHmmss}";
        foreach (char c in Path.GetInvalidFileNameChars()) baseName = baseName.Replace(c, '_');
        CurrentFilePath = Path.Combine(RecordFolder, $"{baseName}.mp3");
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        IsRecording = true;
        RecordingStateChanged?.Invoke(true, CurrentFilePath);

        Task.Run(async () =>
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, streamUrl);
                using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                if (!response.IsSuccessStatusCode) { StopRecording(); return; }
                using var stream = await response.Content.ReadAsStreamAsync(token);
                using var fs = new FileStream(CurrentFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                byte[] buffer = new byte[8192]; int read;
                while ((read = await stream.ReadAsync(buffer, token)) > 0) await fs.WriteAsync(buffer.AsMemory(0, read), token);
            }
            catch { }
            finally { IsRecording = false; RecordingStateChanged?.Invoke(false, CurrentFilePath ?? ""); }
        }, token);
    }
    public void StopRecording() { _cts?.Cancel(); _cts?.Dispose(); _cts = null; IsRecording = false; RecordingStateChanged?.Invoke(false, CurrentFilePath ?? ""); }
    public void Dispose() => StopRecording();
}
