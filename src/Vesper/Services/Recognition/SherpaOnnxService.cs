using System.IO;
using NAudio.Wave;
using SherpaOnnx;
using Vesper.Models;

namespace Vesper.Services.Recognition;

public sealed class SherpaOnnxService : ISpeechRecognitionService
{
    private OfflineRecognizer? _recognizer;
    private string? _loadedModelId;

    public bool IsAvailable => _recognizer != null;

    public void LoadModel(ModelDefinition model)
    {
        if (_loadedModelId == model.Id && _recognizer != null)
            return;

        _recognizer?.Dispose();
        _recognizer = null;
        _loadedModelId = null;

        // Validate all model files exist and are not truncated before calling native code
        foreach (var file in model.Files)
        {
            var path = model.GetFilePath(file.RelativePath);
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    $"Model file missing: {file.RelativePath}. Please re-download the model.", path);

            var fileInfo = new FileInfo(path);
            if (fileInfo.Length < 100)
                throw new InvalidOperationException(
                    $"Model file '{file.RelativePath}' appears corrupted (only {fileInfo.Length} bytes). " +
                    $"Please delete the models/{model.Id} folder and re-download.");
        }

        var config = new OfflineRecognizerConfig();
        config.FeatConfig.SampleRate = 16000;
        config.FeatConfig.FeatureDim = 80;
        config.ModelConfig.Tokens = Path.Combine(model.ModelDirectory, "tokens.txt");
        config.ModelConfig.NumThreads = Math.Max(1, Environment.ProcessorCount / 2);
        config.ModelConfig.Provider = "cpu";
        config.ModelConfig.Debug = 0;
        config.ModelConfig.ModelType = "";
        config.ModelConfig.ModelingUnit = "";
        config.ModelConfig.BpeVocab = "";
        config.ModelConfig.TeleSpeechCtc = "";
        config.DecodingMethod = "greedy_search";
        config.MaxActivePaths = 4;
        config.HotwordsFile = "";
        config.HotwordsScore = 0;
        config.RuleFsts = "";
        config.RuleFars = "";

        switch (model.SherpaType)
        {
            case SherpaModelType.Moonshine:
                var dir = model.ModelDirectory;
                config.ModelConfig.Moonshine.Preprocessor = Path.Combine(dir, "preprocess.onnx");
                config.ModelConfig.Moonshine.Encoder = Path.Combine(dir, "encode.int8.onnx");
                config.ModelConfig.Moonshine.UncachedDecoder = Path.Combine(dir, "uncached_decode.int8.onnx");
                config.ModelConfig.Moonshine.CachedDecoder = Path.Combine(dir, "cached_decode.int8.onnx");
                break;

            case SherpaModelType.SenseVoice:
                config.ModelConfig.SenseVoice.Model = Path.Combine(model.ModelDirectory, "model.int8.onnx");
                config.ModelConfig.SenseVoice.UseInverseTextNormalization = 1;
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown SherpaOnnx model type for '{model.DisplayName}'. SherpaType is not set.");
        }

        try
        {
            _recognizer = new OfflineRecognizer(config);
        }
        catch (Exception ex)
        {
            _recognizer = null;
            _loadedModelId = null;
            throw new InvalidOperationException(
                $"Failed to load SherpaOnnx model '{model.DisplayName}'. " +
                $"Ensure the model files are complete and VC++ Redistributable is installed. " +
                $"Details: {ex.Message}", ex);
        }

        _loadedModelId = model.Id;
    }

    public async Task<string> TranscribeAsync(byte[] wavAudio, string language, CancellationToken ct = default)
    {
        if (_recognizer == null)
            throw new InvalidOperationException(
                "SherpaOnnx model failed to load. Check the status bar for details, " +
                "or try switching to a Whisper model in Settings.");

        var recognizer = _recognizer;

        return await Task.Run(() =>
        {
            // Parse WAV bytes into float samples using NAudio
            using var ms = new MemoryStream(wavAudio);
            using var reader = new WaveFileReader(ms);
            var sampleProvider = reader.ToSampleProvider();

            var sampleCount = (int)reader.SampleCount;
            var samples = new float[sampleCount];
            int read = sampleProvider.Read(samples, 0, sampleCount);

            if (read <= 0) return string.Empty;

            var stream = recognizer.CreateStream();
            stream.AcceptWaveform(reader.WaveFormat.SampleRate, samples[..read]);
            recognizer.Decode(stream);

            var text = stream.Result.Text;
            return text?.Trim() ?? string.Empty;
        }, ct);
    }

    public void Dispose()
    {
        _recognizer?.Dispose();
        _recognizer = null;
        _loadedModelId = null;
    }
}
