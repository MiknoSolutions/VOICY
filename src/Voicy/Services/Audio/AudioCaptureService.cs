using System.IO;
using NAudio.Wave;

namespace Voicy.Services.Audio;

public sealed class AudioCaptureService : IAudioCaptureService
{
    private WaveInEvent? _waveIn;
    private MemoryStream? _buffer;
    private WaveFileWriter? _writer;
    private int _deviceIndex;
    private readonly object _lock = new();

    private const int SampleRate = 16000;
    private const int Channels = 1;
    private const int BitsPerSample = 16;

    public event EventHandler<float[]>? DataAvailable;

    public void SetDevice(int deviceIndex) => _deviceIndex = deviceIndex;

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

    public void StartRecording()
    {
        lock (_lock)
        {
            StopInternal();

            _buffer = new MemoryStream();
            var format = new WaveFormat(SampleRate, BitsPerSample, Channels);
            _writer = new WaveFileWriter(_buffer, format);

            _waveIn = new WaveInEvent
            {
                WaveFormat = format,
                DeviceNumber = _deviceIndex,
                BufferMilliseconds = 100
            };

            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.StartRecording();
        }
    }

    public byte[] StopRecording()
    {
        lock (_lock)
        {
            if (_waveIn == null || _buffer == null || _writer == null)
                return [];

            _waveIn.StopRecording();
            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.Dispose();
            _waveIn = null;

            _writer.Flush();
            // WaveFileWriter writes header on flush; we need to finalize it
            var position = _buffer.Position;
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

            // Finalize the current WAV file
            _writer.Flush();
            _writer.Dispose();

            var wavBytes = _buffer.ToArray();
            _buffer.Dispose();

            // Start a fresh buffer while keeping the mic recording
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
            _writer?.Write(e.Buffer, 0, e.BytesRecorded);
        }

        // Convert PCM16 to float32 for VAD consumers
        int sampleCount = e.BytesRecorded / 2;
        var floatSamples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            short sample = BitConverter.ToInt16(e.Buffer, i * 2);
            floatSamples[i] = sample / 32768f;
        }

        DataAvailable?.Invoke(this, floatSamples);
    }

    private void StopInternal()
    {
        if (_waveIn != null)
        {
            _waveIn.StopRecording();
            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.Dispose();
            _waveIn = null;
        }
        _writer?.Dispose();
        _writer = null;
        _buffer?.Dispose();
        _buffer = null;
    }

    public void Dispose() => StopInternal();
}
