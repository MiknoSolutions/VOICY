using System.IO;
using NAudio.Wave;

namespace Vesper.Services.Audio;

public sealed class AudioCaptureService : IAudioCaptureService
{
    private WaveInEvent? _waveIn;
    private MemoryStream? _buffer;
    private WaveFileWriter? _writer;
    private int _deviceIndex;
    private readonly object _lock = new();
    private bool _isRecording;

    private const int SampleRate = 16000;
    private const int Channels = 1;
    private const int BitsPerSample = 16;

    public event EventHandler<float[]>? DataAvailable;

    public void SetDevice(int deviceIndex)
    {
        if (_deviceIndex != deviceIndex)
        {
            _deviceIndex = deviceIndex;
            // If currently recording, restart with new device
            if (_isRecording)
            {
                var wasRecording = _isRecording;
                StopInternal();
                if (wasRecording) StartRecording();
            }
            else
            {
                // Dispose old device so it's recreated next time
                DisposeWaveIn();
            }
        }
    }

    public List<string> GetAvailableDevices()
    {
        var devices = new List<string>();
        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            var caps = WaveInEvent.GetCapabilities(i);
            devices.Add(caps.ProductName);
        }
        return devices;
    }

    public void PrewarmMicrophone()
    {
        // Pre-create the WaveInEvent object (but don't start recording)
        // This saves ~100-200ms on first hotkey press
        lock (_lock)
        {
            EnsureWaveIn();
        }
    }

    private void EnsureWaveIn()
    {
        if (_waveIn != null) return;

        var format = new WaveFormat(SampleRate, BitsPerSample, Channels);
        _waveIn = new WaveInEvent
        {
            WaveFormat = format,
            DeviceNumber = _deviceIndex,
            BufferMilliseconds = 50 // smaller buffer = lower latency
        };
        _waveIn.DataAvailable += OnDataAvailable;
    }

    public void StartRecording()
    {
        lock (_lock)
        {
            EnsureWaveIn();

            _buffer = new MemoryStream();
            var fmt = new WaveFormat(SampleRate, BitsPerSample, Channels);
            _writer = new WaveFileWriter(_buffer, fmt);
            _isRecording = true;

            _waveIn!.StartRecording();
        }
    }

    public byte[] StopRecording()
    {
        lock (_lock)
        {
            if (_buffer == null || _writer == null)
                return [];

            _isRecording = false;

            _waveIn?.StopRecording();

            _writer.Flush();
            _writer.Dispose();
            _writer = null;

            var wavBytes = _buffer.ToArray();
            _buffer.Dispose();
            _buffer = null;

            return wavBytes;
        }
    }

    public byte[] FlushBuffer()
    {
        lock (_lock)
        {
            if (_waveIn == null || _buffer == null || _writer == null)
                return [];

            _writer.Flush();
            _writer.Dispose();

            var wavBytes = _buffer.ToArray();
            _buffer.Dispose();

            _buffer = new MemoryStream();
            var format = new WaveFormat(SampleRate, BitsPerSample, Channels);
            _writer = new WaveFileWriter(_buffer, format);

            return wavBytes;
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        lock (_lock)
        {
            if (_isRecording)
                _writer?.Write(e.Buffer, 0, e.BytesRecorded);
        }

        // Convert PCM16 to float32 for VAD / streaming consumers
        int sampleCount = e.BytesRecorded / 2;
        var floatSamples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            short sample = BitConverter.ToInt16(e.Buffer, i * 2);
            floatSamples[i] = sample / 32768f;
        }

        DataAvailable?.Invoke(this, floatSamples);
    }

    private void DisposeWaveIn()
    {
        if (_waveIn != null)
        {
            try { _waveIn.StopRecording(); } catch { }
            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.Dispose();
            _waveIn = null;
        }
    }

    private void StopInternal()
    {
        _isRecording = false;
        _writer?.Dispose();
        _writer = null;
        _buffer?.Dispose();
        _buffer = null;
        DisposeWaveIn();
    }

    public void Dispose() => StopInternal();
}
