using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace RADYJKO;

public class SleepTimer : IDisposable
{
    private CancellationTokenSource? _cts;
    public bool IsActive => _cts != null;
    public int SecondsRemaining { get; private set; }
    public event Action<int>? Tick;
    public event Action? Elapsed;

    public void Start(int minutes, Action<float> setVolumeFunc, float currentVolume, bool shutdownPc = false)
    {
        Stop();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        SecondsRemaining = minutes * 60;
        Task.Run(async () =>
        {
            try
            {
                while (SecondsRemaining > 0)
                {
                    token.ThrowIfCancellationRequested();
                    await Task.Delay(1000, token);
                    SecondsRemaining--;
                    Tick?.Invoke(SecondsRemaining);
                    if (SecondsRemaining <= 30) setVolumeFunc(currentVolume * ((float)SecondsRemaining / 30f));
                }
                Elapsed?.Invoke();
                if (shutdownPc) Process.Start("shutdown", "/s /t 60");
            }
            catch (OperationCanceledException) { }
        }, token);
    }
    public void Stop() { _cts?.Cancel(); _cts?.Dispose(); _cts = null; SecondsRemaining = 0; Tick?.Invoke(0); }
    public void Dispose() => Stop();
}
