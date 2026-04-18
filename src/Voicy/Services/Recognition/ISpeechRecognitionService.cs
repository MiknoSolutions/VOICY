namespace Voicy.Services.Recognition;

public interface ISpeechRecognitionService : IDisposable
{
    Task<string> TranscribeAsync(byte[] wavAudio, string language, CancellationToken ct = default);
    bool IsAvailable { get; }
}
