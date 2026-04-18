using System.IO;
using NAudio.Wave;
using SherpaOnnx;
using Voicy.Models;

namespace Voicy.Services.Recognition;

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

        var config = new OfflineRecognizerConfig();
        config.ModelConfig.Tokens = Path.Combine(model.ModelDirectory, "tokens.txt");
        config.ModelConfig.NumThreads = Math.Max(1, Environment.ProcessorCount / 2);

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
        }

        _recognizer = new OfflineRecognizer(config);
        _loadedModelId = model.Id;
    }

    public async Task<string> TranscribeAsync(byte[] wavAudio, string language, CancellationToken ct = default)
    {
        if (_recognizer == null)
            throw new InvalidOperationException("Model not loaded. Call LoadModel first.");

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
