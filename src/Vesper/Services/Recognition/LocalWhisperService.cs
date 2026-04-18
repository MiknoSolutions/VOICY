using System.IO;
using System.Text;
using Whisper.net;

namespace Vesper.Services.Recognition;

public sealed class LocalWhisperService : ISpeechRecognitionService
{
    private WhisperFactory? _factory;
    private string? _loadedModelPath;

    public bool IsAvailable => _factory != null;

    public void LoadModel(string modelPath)
    {
        if (_loadedModelPath == modelPath && _factory != null)
            return;

        if (!File.Exists(modelPath))
            throw new FileNotFoundException(
                $"Model file not found: {modelPath}. Please re-download the model.", modelPath);

        _factory?.Dispose();
        _factory = null;
        _loadedModelPath = null;

        try
        {
            _factory = WhisperFactory.FromPath(modelPath);
            _loadedModelPath = modelPath;
        }
        catch (Exception ex)
        {
            _factory = null;
            throw new InvalidOperationException(
                $"Failed to load Whisper model from '{Path.GetFileName(modelPath)}'. " +
                $"The file may be corrupted — try re-downloading. Details: {ex.Message}", ex);
        }
    }

    public async Task<string> TranscribeAsync(byte[] wavAudio, string language, CancellationToken ct = default)
    {
        if (_factory == null)
            throw new InvalidOperationException(
                "Whisper model failed to load. Check the status bar for details, " +
                "or try re-downloading the model in Settings.");

        var builder = _factory.CreateBuilder()
            .WithThreads(Environment.ProcessorCount > 1 ? Environment.ProcessorCount / 2 : 1);

        if (!string.IsNullOrEmpty(language) && language != "auto")
        {
            builder.WithLanguage(language);
        }
        else
        {
            builder.WithLanguageDetection();
        }

        using var processor = builder.Build();

        using var ms = new MemoryStream(wavAudio);
        var sb = new StringBuilder();

        await foreach (var segment in processor.ProcessAsync(ms, ct))
        {
            sb.Append(segment.Text);
        }

        return sb.ToString().Trim();
    }

    public void Dispose()
    {
        _factory?.Dispose();
        _factory = null;
    }
}
