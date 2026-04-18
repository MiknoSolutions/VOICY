using System.IO;
using NAudio.Wave;
using SherpaOnnx;
using Vesper.Models;

namespace Vesper.Services.Recognition;

public sealed class SherpaOnnxService : ISpeechRecognitionService
{
    private OfflineRecognizer? _recognizer;
    private string? _loadedModelId;
    private string? _safeCopyDir; // temp dir used when original path has non-ASCII chars

    public bool IsAvailable => _recognizer != null;

    /// <summary>
    /// SherpaOnnx marshals file paths as ANSI (LPStr). Non-ASCII characters
    /// in the path (e.g. ł, ń, ö) get corrupted, causing the native library
    /// to fail loading the model. When this happens, copy model files to a
    /// temp directory with an ASCII-only path.
    /// </summary>
    private static bool NeedsAsciiSafePath(string path)
    {
        foreach (var c in path)
        {
            if (c > 127) return true;
        }
        return false;
    }

    private string EnsureAsciiSafePath(ModelDefinition model)
    {
        var originalDir = model.ModelDirectory;
        if (!NeedsAsciiSafePath(originalDir))
            return originalDir;

        // Use a stable temp path: %TEMP%/vesper-models/<model-id>
        var tempBase = Path.Combine(Path.GetTempPath(), "vesper-models", model.Id);
        if (!Directory.Exists(tempBase))
            Directory.CreateDirectory(tempBase);

        foreach (var file in model.Files)
        {
            var src = model.GetFilePath(file.RelativePath);
            var dst = Path.Combine(tempBase, file.RelativePath);
            if (!File.Exists(dst) || new FileInfo(dst).Length != new FileInfo(src).Length)
                File.Copy(src, dst, overwrite: true);
        }

        _safeCopyDir = tempBase;
        return tempBase;
    }

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
        var modelDir = EnsureAsciiSafePath(model);
        config.FeatConfig.SampleRate = 16000;
        config.FeatConfig.FeatureDim = 80;
        config.ModelConfig.Tokens = Path.Combine(modelDir, "tokens.txt");
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
                config.ModelConfig.Moonshine.Preprocessor = Path.Combine(modelDir, "preprocess.onnx");
                config.ModelConfig.Moonshine.Encoder = Path.Combine(modelDir, "encode.int8.onnx");
                config.ModelConfig.Moonshine.UncachedDecoder = Path.Combine(modelDir, "uncached_decode.int8.onnx");
                config.ModelConfig.Moonshine.CachedDecoder = Path.Combine(modelDir, "cached_decode.int8.onnx");
                break;

            case SherpaModelType.SenseVoice:
                config.ModelConfig.SenseVoice.Model = Path.Combine(modelDir, "model.int8.onnx");
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
