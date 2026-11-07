using System;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace RADYJKO;

public class AudioPlayer : IDisposable
{
    private WaveOutEvent? _outputDevice;
    private MediaFoundationReader? _reader;
    private AudioDspSampleProvider? _dspProvider;
    private WaveOutEvent? _fadingOutDevice;
    private MediaFoundationReader? _fadingOutReader;
    private readonly IcyMetadataClient _metadataClient = new();
    private CancellationTokenSource? _cts;
    private string? _currentUrl;

    public float CurrentVolume { get; private set; } = 0.75f;
    public bool IsAutoLevelerEnabled { get; set; } = true;
    public float[] BandGains { get; } = new float[12];

    public event Action<string>? PlaybackStateChanged;
    public event Action<string>? TrackChanged;
    public event Action<StreamDiagnostics>? DiagnosticsChanged;
    public event Action<float[]>? SpectrumData;
    public event Action<Exception>? PlaybackError;

    public bool IsPlaying => _outputDevice?.PlaybackState == PlaybackState.Playing;

    public AudioPlayer()
    {
        _metadataClient.TitleReceived += title => TrackChanged?.Invoke(title);
        _metadataClient.DiagnosticsReceived += diag => DiagnosticsChanged?.Invoke(diag);
        _metadataClient.StreamInterrupted += async () =>
        {
            PlaybackStateChanged?.Invoke("Wznawianie...");
            if (!string.IsNullOrEmpty(_currentUrl)) { await Task.Delay(2500); await PlayAsync(_currentUrl); }
        };
    }

    public async Task PlayAsync(string url)
    {
        TriggerCrossfadeOut();
        _currentUrl = url;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        PlaybackStateChanged?.Invoke("Rozwiązywanie...");
        TrackChanged?.Invoke("Ładowanie...");

        try
        {
            _currentUrl = await StreamResolver.ResolveUrlAsync(url);
            PlaybackStateChanged?.Invoke("Łączenie...");

            await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                _reader = new MediaFoundationReader(_currentUrl);

                var sampleProvider = _reader.ToSampleProvider();
                _dspProvider = new AudioDspSampleProvider(sampleProvider) { IsAutoLevelerEnabled = IsAutoLevelerEnabled };
                for (int i = 0; i < 12; i++) _dspProvider.BandGains[i] = BandGains[i];
                _dspProvider.UpdateFilters();
                _dspProvider.FftCalculated += data => SpectrumData?.Invoke(data);

                _outputDevice = new WaveOutEvent { DesiredLatency = 300 };
                _outputDevice.Init(_dspProvider);
                _outputDevice.Volume = 0.0f;
                _outputDevice.Play();
            }, token);

            _ = FadeVolumeAsync(_outputDevice, 0.0f, CurrentVolume, 1500);
            PlaybackStateChanged?.Invoke("Na żywo");
            _metadataClient.StartMonitoring(_currentUrl);
        }
        catch (OperationCanceledException) { PlaybackStateChanged?.Invoke("Zatrzymano"); }
        catch (Exception ex)
        {
            Stop();
            PlaybackStateChanged?.Invoke("Błąd");
            PlaybackError?.Invoke(ex);
        }
    }

    private void TriggerCrossfadeOut()
    {
        _metadataClient.Stop();
        if (_outputDevice != null && _outputDevice.PlaybackState == PlaybackState.Playing)
        {
            var dev = _outputDevice; var reader = _reader; var startVol = dev.Volume;
            _fadingOutDevice = dev; _fadingOutReader = reader;
            _outputDevice = null; _reader = null;
            Task.Run(async () =>
            {
                await FadeVolumeAsync(dev, startVol, 0.0f, 1500);
                try { dev.Stop(); dev.Dispose(); reader?.Dispose(); } catch {}
            });
        }
        else { Stop(); }
    }

    private async Task FadeVolumeAsync(WaveOutEvent? device, float start, float target, int durationMs)
    {
        if (device == null) return;
        int steps = 20; int delay = durationMs / steps; float diff = target - start;
        for (int i = 1; i <= steps; i++)
        {
            if (device.PlaybackState != PlaybackState.Playing) break;
            try { device.Volume = start + (diff * i / steps); } catch { break; }
            await Task.Delay(delay);
        }
    }

    public void SetAutoLeveler(bool enabled) { IsAutoLevelerEnabled = enabled; if (_dspProvider != null) _dspProvider.IsAutoLevelerEnabled = enabled; }
    public void ApplyBandGain(int bandIndex, float gainDb)
    {
        if (bandIndex >= 0 && bandIndex < 12)
        {
            BandGains[bandIndex] = gainDb;
            if (_dspProvider != null) { _dspProvider.BandGains[bandIndex] = gainDb; _dspProvider.UpdateFilters(); }
        }
    }
    public void SetVolume(float volume) { CurrentVolume = Math.Clamp(volume, 0f, 1f); if (_outputDevice != null) _outputDevice.Volume = CurrentVolume; }
    public void Stop()
    {
        _cts?.Cancel(); _cts?.Dispose(); _cts = null;
        _metadataClient.Stop();
        if (_outputDevice != null) { _outputDevice.Stop(); _outputDevice.Dispose(); _outputDevice = null; }
        if (_reader != null) { _reader.Dispose(); _reader = null; }
        _dspProvider = null;
        PlaybackStateChanged?.Invoke("Zatrzymano");
        TrackChanged?.Invoke(string.Empty);
    }
    public void Dispose() { Stop(); _metadataClient.Dispose(); }
}
