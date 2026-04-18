namespace Voicy.Services.Audio;

public sealed class EnergyVoiceActivityDetector : IVoiceActivityDetector
{
    private float _threshold;
    private bool _isSpeaking;
    private DateTime _speechStart;
    private DateTime _lastSpeechTime;
    private readonly List<float> _speechBuffer = new();
    private readonly object _lock = new();

    public event EventHandler? SpeechStarted;
    public event EventHandler<float[]>? SpeechEnded;

    public float ThresholdDb
    {
        get => 20f * MathF.Log10(_threshold);
        set => _threshold = MathF.Pow(10f, value / 20f);
    }

    public int SilenceDurationMs { get; set; } = 800;
    public int MinSpeechDurationMs { get; set; } = 300;

    public EnergyVoiceActivityDetector()
    {
        ThresholdDb = -30f;
    }

    public void ProcessAudioFrame(float[] samples)
    {
        float rms = CalculateRms(samples);
        bool aboveThreshold = rms > _threshold;

        lock (_lock)
        {
            if (aboveThreshold)
            {
                _lastSpeechTime = DateTime.UtcNow;

                if (!_isSpeaking)
                {
                    _speechStart = DateTime.UtcNow;
                    _isSpeaking = true;
                    _speechBuffer.Clear();
                    SpeechStarted?.Invoke(this, EventArgs.Empty);
                }

                _speechBuffer.AddRange(samples);
            }
            else if (_isSpeaking)
            {
                // Keep buffering during short silence gaps
                _speechBuffer.AddRange(samples);

                double silenceMs = (DateTime.UtcNow - _lastSpeechTime).TotalMilliseconds;
                double speechMs = (DateTime.UtcNow - _speechStart).TotalMilliseconds;

                if (silenceMs >= SilenceDurationMs && speechMs >= MinSpeechDurationMs)
                {
                    _isSpeaking = false;
                    var speechData = _speechBuffer.ToArray();
                    _speechBuffer.Clear();
                    SpeechEnded?.Invoke(this, speechData);
                }
            }
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _isSpeaking = false;
            _speechBuffer.Clear();
        }
    }

    private static float CalculateRms(float[] samples)
    {
        if (samples.Length == 0) return 0f;
        float sum = 0f;
        foreach (var s in samples)
            sum += s * s;
        return MathF.Sqrt(sum / samples.Length);
    }
}
