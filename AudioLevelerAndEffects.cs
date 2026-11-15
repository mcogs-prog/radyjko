using System;
using NAudio.Dsp;
using NAudio.Wave;

namespace RADYJKO;

public class AudioDspSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly BiQuadFilter[][] _filters;
    private readonly float[] _fftBuffer = new float[1024];
    private int _fftPos;

    private float _currentGain = 1.0f;
    private const float TargetRms = 0.18f;
    private const float MaxGain = 3.2f;
    private const float MinGain = 0.4f;

    public bool IsAutoLevelerEnabled { get; set; } = true;
    public bool IsNightCompressorEnabled { get; set; } = false;
    public float[] BandGains { get; } = new float[12];
    public static readonly float[] Frequencies = { 31.5f, 63f, 125f, 250f, 500f, 1000f, 2000f, 4000f, 8000f, 12000f, 14000f, 16000f };

    // Time-Shifting Ring Buffer (stores last ~3 minutes of PCM float samples at 44.1kHz stereo = ~16M floats ~64MB)
    private readonly float[] _timeShiftRing;
    private long _writeCursor = 0;
    private long _totalWritten = 0;
    private long _readCursor = 0;
    public bool IsTimeShifted { get; private set; } = false;
    public bool IsTimeShiftPaused { get; set; } = false;

    public int SecondsBehind => WaveFormat.SampleRate > 0 && WaveFormat.Channels > 0
        ? (int)Math.Max(0, (_writeCursor - _readCursor) / (WaveFormat.SampleRate * WaveFormat.Channels))
        : 0;

    public int MaxBufferedSeconds => WaveFormat.SampleRate > 0 && WaveFormat.Channels > 0
        ? (int)(Math.Min(_totalWritten, _timeShiftRing.Length) / (WaveFormat.SampleRate * WaveFormat.Channels))
        : 0;

    public event Action<float[]>? FftCalculated;
    public event Action<float, float>? VuMeterCalculated;
    public WaveFormat WaveFormat => _source.WaveFormat;

    public AudioDspSampleProvider(ISampleProvider source)
    {
        _source = source;
        int channels = source.WaveFormat.Channels;
        int sr = Math.Max(44100, source.WaveFormat.SampleRate);
        // 5 minutes ring buffer
        _timeShiftRing = new float[sr * Math.Max(1, channels) * 300];
        _filters = new BiQuadFilter[channels][];
        for (int c = 0; c < channels; c++) _filters[c] = new BiQuadFilter[12];
        UpdateFilters();
    }

    public void UpdateFilters()
    {
        int sampleRate = WaveFormat.SampleRate;
        int channels = WaveFormat.Channels;
        for (int ch = 0; ch < channels; ch++)
        {
            for (int b = 0; b < 12; b++)
            {
                _filters[ch][b] = BiQuadFilter.PeakingEQ(sampleRate, Frequencies[b], 1.414f, BandGains[b]);
            }
        }
    }

    public void RewindSeconds(int seconds)
    {
        long delta = (long)seconds * WaveFormat.SampleRate * WaveFormat.Channels;
        long minAllowed = Math.Max(0, _writeCursor - _timeShiftRing.Length + 4096);
        _readCursor = Math.Max(minAllowed, _readCursor - delta);
        IsTimeShifted = (_writeCursor - _readCursor) > (WaveFormat.SampleRate * WaveFormat.Channels);
    }

    public void ForwardSeconds(int seconds)
    {
        long delta = (long)seconds * WaveFormat.SampleRate * WaveFormat.Channels;
        _readCursor = Math.Min(_writeCursor, _readCursor + delta);
        if ((_writeCursor - _readCursor) <= (WaveFormat.SampleRate * WaveFormat.Channels))
        {
            _readCursor = _writeCursor;
            IsTimeShifted = false;
            IsTimeShiftPaused = false;
        }
    }

    public void JumpToLive()
    {
        _readCursor = _writeCursor;
        IsTimeShifted = false;
        IsTimeShiftPaused = false;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);
        if (read <= 0) return read;

        int channels = WaveFormat.Channels;
        int ringLen = _timeShiftRing.Length;

        // 1. Always record live incoming stream into ring buffer
        for (int i = 0; i < read; i++)
        {
            _timeShiftRing[_writeCursor % ringLen] = buffer[offset + i];
            _writeCursor++;
            _totalWritten++;
        }

        // 2. If paused in Time-Shift mode, output silence while live buffer keeps filling!
        if (IsTimeShiftPaused)
        {
            IsTimeShifted = true;
            Array.Clear(buffer, offset, read);
            VuMeterCalculated?.Invoke(0f, 0f);
            return read;
        }

        // 3. Read either live or delayed samples from ring buffer
        if (!IsTimeShifted)
        {
            _readCursor = _writeCursor;
        }
        else
        {
            long minAllowed = Math.Max(0, _writeCursor - ringLen + 4096);
            if (_readCursor < minAllowed) _readCursor = minAllowed;

            for (int i = 0; i < read; i++)
            {
                if (_readCursor < _writeCursor)
                {
                    buffer[offset + i] = _timeShiftRing[_readCursor % ringLen];
                    _readCursor++;
                }
                else
                {
                    IsTimeShifted = false;
                    break;
                }
            }
        }

        float sumSquare = 0f;
        float peakL = 0f, peakR = 0f;

        for (int i = 0; i < read; i++)
        {
            float s = buffer[offset + i];
            sumSquare += s * s;
        }

        if (IsAutoLevelerEnabled || IsNightCompressorEnabled)
        {
            float rms = MathF.Sqrt(sumSquare / read);
            if (rms > 0.005f)
            {
                float target = IsNightCompressorEnabled ? 0.14f : TargetRms;
                float maxG = IsNightCompressorEnabled ? 4.2f : MaxGain;
                float desiredGain = Math.Clamp(target / rms, MinGain, maxG);
                float speed = IsNightCompressorEnabled ? 0.08f : 0.02f;
                _currentGain += (desiredGain - _currentGain) * speed;
            }
        }
        else { _currentGain = 1.0f; }

        for (int i = 0; i < read; i++)
        {
            int ch = i % channels;
            float sample = buffer[offset + i] * _currentGain;
            for (int b = 0; b < 12; b++) sample = _filters[ch][b].Transform(sample);

            // Night Compressor soft-knee brickwall limiter (tames loud commercials & peaks)
            if (IsNightCompressorEnabled)
            {
                const float threshold = 0.38f;
                const float ratio = 0.22f;
                float absS = Math.Abs(sample);
                if (absS > threshold)
                {
                    float over = absS - threshold;
                    float compressed = threshold + over * ratio;
                    sample = Math.Sign(sample) * compressed;
                }
            }

            sample = Math.Clamp(sample, -1.0f, 1.0f);
            buffer[offset + i] = sample;

            float abs = Math.Abs(sample);
            if (ch == 0 && abs > peakL) peakL = abs;
            if (ch == 1 && abs > peakR) peakR = abs;

            _fftBuffer[_fftPos++] = sample;
            if (_fftPos >= _fftBuffer.Length) { _fftPos = 0; CalculateVisualizer(); }
        }

        if (channels == 1) peakR = peakL;
        VuMeterCalculated?.Invoke(peakL, peakR);
        return read;
    }

    private void CalculateVisualizer()
    {
        float[] bands = new float[12];
        int step = _fftBuffer.Length / 12;
        for (int b = 0; b < 12; b++)
        {
            float sum = 0f;
            for (int i = 0; i < step; i++) sum += Math.Abs(_fftBuffer[b * step + i]);
            bands[b] = Math.Clamp((sum / step) * 4.8f, 0.04f, 1f);
        }
        FftCalculated?.Invoke(bands);
    }
}
