namespace Voicy.Services.Audio;

public interface IVoiceActivityDetector
{
    event EventHandler? SpeechStarted;
    event EventHandler<float[]>? SpeechEnded;
    float ThresholdDb { get; set; }
    int SilenceDurationMs { get; set; }
    int MinSpeechDurationMs { get; set; }
    void ProcessAudioFrame(float[] samples);
    void Reset();
}
