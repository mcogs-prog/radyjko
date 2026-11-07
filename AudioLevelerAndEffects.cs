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
    public float[] BandGains { get; } = new float[12];
    public static readonly float[] Frequencies = { 31.5f, 63f, 125f, 250f, 500f, 1000f, 2000f, 4000f, 8000f, 12000f, 14000f, 16000f };

    public event Action<float[]>? FftCalculated;
    public WaveFormat WaveFormat => _source.WaveFormat;

    public AudioDspSampleProvider(ISampleProvider source)
    {
        _source = source;
        int channels = source.WaveFormat.Channels;
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

    public int Read(float[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);
        int channels = WaveFormat.Channels;
        float sumSquare = 0f;
        for (int i = 0; i < read; i++)
        {
            float s = buffer[offset + i];
            sumSquare += s * s;
        }

        if (IsAutoLevelerEnabled && read > 0)
        {
            float rms = MathF.Sqrt(sumSquare / read);
            if (rms > 0.01f)
            {
                float desiredGain = Math.Clamp(TargetRms / rms, MinGain, MaxGain);
                _currentGain += (desiredGain - _currentGain) * 0.02f;
            }
        }
        else { _currentGain = 1.0f; }

        for (int i = 0; i < read; i++)
        {
            int ch = i % channels;
            float sample = buffer[offset + i] * _currentGain;
            for (int b = 0; b < 12; b++) sample = _filters[ch][b].Transform(sample);
            buffer[offset + i] = Math.Clamp(sample, -1.0f, 1.0f);

            _fftBuffer[_fftPos++] = sample;
            if (_fftPos >= _fftBuffer.Length) { _fftPos = 0; CalculateVisualizer(); }
        }
        return read;
    }

    private void CalculateVisualizer()
    {
        float[] bands = new float[8];
        int step = _fftBuffer.Length / 8;
        for (int b = 0; b < 8; b++)
        {
            float sum = 0f;
            for (int i = 0; i < step; i++) sum += Math.Abs(_fftBuffer[b * step + i]);
            bands[b] = Math.Clamp((sum / step) * 4.5f, 0.05f, 1f);
        }
        FftCalculated?.Invoke(bands);
    }
}
