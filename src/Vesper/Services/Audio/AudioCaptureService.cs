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
    private bool _isPrewarmed;

    private const int SampleRate = 16000;
    private const int Channels = 1;
    private const int BitsPerSample = 16;

    public event EventHandler<float[]>? DataAvailable;

    public void SetDevice(int deviceIndex)
    {
        if (_deviceIndex != deviceIndex)
        {
            _deviceIndex = deviceIndex;
            // Re-prewarm with new device
            if (_isPrewarmed)
            {
                StopMicrophone();
                PrewarmMicrophone();
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

    /// <summary>
    /// Keep the microphone device open and listening so StartRecording has zero latency.
    /// </summary>
    public void PrewarmMicrophone()
    {
        lock (_lock)
        {
            if (_isPrewarmed) return;

            var format = new WaveFormat(SampleRate, BitsPerSample, Channels);
            _waveIn = new WaveInEvent
            {
                WaveFormat = format,
                DeviceNumber = _deviceIndex,
                BufferMilliseconds = 100
            };

            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.StartRecording();
            _isPrewarmed = true;
        }
    }

    private void StopMicrophone()
    {
        lock (_lock)
        {
            if (_waveIn != null)
            {
                _waveIn.StopRecording();
                _waveIn.DataAvailable -= OnDataAvailable;
                _waveIn.Dispose();
                _waveIn = null;
            }
            _isPrewarmed = false;
        }
    }

    public void StartRecording()
    {
        lock (_lock)
        {
            // Ensure microphone is running
            if (!_isPrewarmed)
            {
                var format = new WaveFormat(SampleRate, BitsPerSample, Channels);
                _waveIn = new WaveInEvent
                {
                    WaveFormat = format,
                    DeviceNumber = _deviceIndex,
                    BufferMilliseconds = 100
                };
                _waveIn.DataAvailable += OnDataAvailable;
                _waveIn.StartRecording();
                _isPrewarmed = true;
            }

            // Start capturing to buffer
            _buffer = new MemoryStream();
            var fmt = new WaveFormat(SampleRate, BitsPerSample, Channels);
            _writer = new WaveFileWriter(_buffer, fmt);
            _isRecording = true;
        }
    }

    public byte[] StopRecording()
    {
        lock (_lock)
        {
            if (_buffer == null || _writer == null)
                return [];

            _isRecording = false;

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

    private void StopInternal()
    {
        _isRecording = false;
        _writer?.Dispose();
        _writer = null;
        _buffer?.Dispose();
        _buffer = null;
        StopMicrophone();
    }

    public void Dispose() => StopInternal();
}
