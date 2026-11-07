using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace RADYJKO;

public class StreamDiagnostics
{
    public string Codec { get; set; } = "MP3";
    public string Bitrate { get; set; } = "128 kbps";
    public string SampleRate { get; set; } = "44.1 kHz";
    public string Status { get; set; } = "Połączono";
}

public class IcyMetadataClient : IDisposable
{
    private CancellationTokenSource? _cts;
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

    public event Action<string>? TitleReceived;
    public event Action<StreamDiagnostics>? DiagnosticsReceived;
    public event Action? StreamInterrupted;

    public void StartMonitoring(string streamUrl)
    {
        Stop();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        Task.Run(async () =>
        {
            int retryCount = 0;
            while (!token.IsCancellationRequested && retryCount < 5)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, streamUrl);
                    request.Version = new Version(1, 1);
                    request.Headers.ConnectionClose = true;
                    request.Headers.Add("Icy-MetaData", "1");
                    request.Headers.Add("User-Agent", "VLC/3.0.0");

                    using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                    if (!response.IsSuccessStatusCode) { retryCount++; await Task.Delay(2000, token); continue; }

                    retryCount = 0;
                    var diag = new StreamDiagnostics();
                    if (response.Headers.TryGetValues("icy-br", out var brVals))
                    {
                        foreach (var b in brVals) { diag.Bitrate = $"{b} kbps"; break; }
                    }
                    if (response.Content.Headers.ContentType != null)
                    {
                        var mediaType = response.Content.Headers.ContentType.MediaType ?? "";
                        if (mediaType.Contains("aac")) diag.Codec = "AAC";
                        else if (mediaType.Contains("ogg")) diag.Codec = "OGG/Opus";
                        else if (mediaType.Contains("flac")) diag.Codec = "FLAC";
                        else diag.Codec = "MP3";
                    }
                    DiagnosticsReceived?.Invoke(diag);

                    int metaInt = 0;
                    if (response.Headers.TryGetValues("icy-metaint", out var values))
                    {
                        foreach (var v in values) if (int.TryParse(v, out int parsed)) { metaInt = parsed; break; }
                    }

                    if (metaInt <= 0) { TitleReceived?.Invoke(string.Empty); return; }

                    using var stream = await response.Content.ReadAsStreamAsync(token);
                    byte[] audioBuffer = new byte[metaInt];
                    byte[] lengthByte = new byte[1];

                    while (!token.IsCancellationRequested)
                    {
                        int totalRead = 0;
                        while (totalRead < metaInt)
                        {
                            int r = await stream.ReadAsync(audioBuffer.AsMemory(totalRead, metaInt - totalRead), token);
                            if (r <= 0) throw new IOException("Przerwano");
                            totalRead += r;
                        }

                        int lenRead = await stream.ReadAsync(lengthByte.AsMemory(0, 1), token);
                        if (lenRead <= 0) break;

                        int metaLen = lengthByte[0] * 16;
                        if (metaLen > 0)
                        {
                            byte[] metaBuffer = new byte[metaLen];
                            int mRead = 0;
                            while (mRead < metaLen)
                            {
                                int r = await stream.ReadAsync(metaBuffer.AsMemory(mRead, metaLen - mRead), token);
                                if (r <= 0) break;
                                mRead += r;
                            }
                            string metaString = Encoding.UTF8.GetString(metaBuffer);
                            var match = Regex.Match(metaString, @"StreamTitle='([^']*)'");
                            if (match.Success) TitleReceived?.Invoke(match.Groups[1].Value.Trim());
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch { StreamInterrupted?.Invoke(); retryCount++; await Task.Delay(3000, token); }
            }
        }, token);
    }

    public void Stop() { _cts?.Cancel(); _cts?.Dispose(); _cts = null; }
    public void Dispose() => Stop();
}
