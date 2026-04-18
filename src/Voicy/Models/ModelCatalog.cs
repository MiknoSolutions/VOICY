using System.IO;

namespace Voicy.Models;

public enum ModelEngine { WhisperNet, SherpaOnnx }
public enum SherpaModelType { Moonshine, SenseVoice }

public record ModelFile(string RelativePath, string DownloadUrl);

public class ModelDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Family { get; init; }
    public required ModelEngine Engine { get; init; }
    public SherpaModelType? SherpaType { get; init; }
    public required ModelFile[] Files { get; init; }
    public required long ApproxSizeMb { get; init; }
    public required string[] Languages { get; init; }

    public string ModelDirectory => Engine == ModelEngine.WhisperNet
        ? Path.Combine(AppContext.BaseDirectory, "models")
        : Path.Combine(AppContext.BaseDirectory, "models", Id);

    public string GetFilePath(string relativePath)
        => Path.Combine(ModelDirectory, relativePath);

    public bool IsDownloaded()
        => Files.All(f => File.Exists(GetFilePath(f.RelativePath)));

    public override string ToString() => DisplayName;
}

public static class ModelCatalog
{
    private const string WhisperBase = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main";
    private const string MoonshineRepo = "https://huggingface.co/csukuangfj/sherpa-onnx-moonshine";
    private const string SenseVoiceRepo = "https://huggingface.co/csukuangfj/sherpa-onnx-sense-voice-zh-en-ja-ko-yue-2024-07-17/resolve/main";

    public static readonly ModelDefinition[] All =
    [
        // ── Whisper models (via Whisper.net / whisper.cpp) ──
        new()
        {
            Id = "whisper-tiny",
            DisplayName = "Whisper — Tiny (~75 MB)",
            Family = "Whisper",
            Engine = ModelEngine.WhisperNet,
            Files = [new("ggml-tiny.bin", $"{WhisperBase}/ggml-tiny.bin")],
            ApproxSizeMb = 75,
            Languages = ["multi"]
        },
        new()
        {
            Id = "whisper-base",
            DisplayName = "Whisper — Base (~142 MB)",
            Family = "Whisper",
            Engine = ModelEngine.WhisperNet,
            Files = [new("ggml-base.bin", $"{WhisperBase}/ggml-base.bin")],
            ApproxSizeMb = 142,
            Languages = ["multi"]
        },
        new()
        {
            Id = "whisper-small",
            DisplayName = "Whisper — Small (~466 MB)",
            Family = "Whisper",
            Engine = ModelEngine.WhisperNet,
            Files = [new("ggml-small.bin", $"{WhisperBase}/ggml-small.bin")],
            ApproxSizeMb = 466,
            Languages = ["multi"]
        },
        new()
        {
            Id = "whisper-medium",
            DisplayName = "Whisper — Medium (~1.5 GB)",
            Family = "Whisper",
            Engine = ModelEngine.WhisperNet,
            Files = [new("ggml-medium.bin", $"{WhisperBase}/ggml-medium.bin")],
            ApproxSizeMb = 1500,
            Languages = ["multi"]
        },
        new()
        {
            Id = "whisper-large",
            DisplayName = "Whisper — Large v3 (~3 GB)",
            Family = "Whisper",
            Engine = ModelEngine.WhisperNet,
            Files = [new("ggml-large-v3.bin", $"{WhisperBase}/ggml-large-v3.bin")],
            ApproxSizeMb = 3000,
            Languages = ["multi"]
        },

        // ── Moonshine models (via SherpaOnnx) — fast, low RAM, English ──
        new()
        {
            Id = "moonshine-tiny-en",
            DisplayName = "Moonshine — Tiny EN (~30 MB)",
            Family = "Moonshine",
            Engine = ModelEngine.SherpaOnnx,
            SherpaType = SherpaModelType.Moonshine,
            Files =
            [
                new("preprocess.onnx", $"{MoonshineRepo}-tiny-en-int8/resolve/main/preprocess.onnx"),
                new("encode.int8.onnx", $"{MoonshineRepo}-tiny-en-int8/resolve/main/encode.int8.onnx"),
                new("uncached_decode.int8.onnx", $"{MoonshineRepo}-tiny-en-int8/resolve/main/uncached_decode.int8.onnx"),
                new("cached_decode.int8.onnx", $"{MoonshineRepo}-tiny-en-int8/resolve/main/cached_decode.int8.onnx"),
                new("tokens.txt", $"{MoonshineRepo}-tiny-en-int8/resolve/main/tokens.txt"),
            ],
            ApproxSizeMb = 30,
            Languages = ["en"]
        },
        new()
        {
            Id = "moonshine-base-en",
            DisplayName = "Moonshine — Base EN (~70 MB)",
            Family = "Moonshine",
            Engine = ModelEngine.SherpaOnnx,
            SherpaType = SherpaModelType.Moonshine,
            Files =
            [
                new("preprocess.onnx", $"{MoonshineRepo}-base-en-int8/resolve/main/preprocess.onnx"),
                new("encode.int8.onnx", $"{MoonshineRepo}-base-en-int8/resolve/main/encode.int8.onnx"),
                new("uncached_decode.int8.onnx", $"{MoonshineRepo}-base-en-int8/resolve/main/uncached_decode.int8.onnx"),
                new("cached_decode.int8.onnx", $"{MoonshineRepo}-base-en-int8/resolve/main/cached_decode.int8.onnx"),
                new("tokens.txt", $"{MoonshineRepo}-base-en-int8/resolve/main/tokens.txt"),
            ],
            ApproxSizeMb = 70,
            Languages = ["en"]
        },

        // ── SenseVoice (via SherpaOnnx) — multilingual ──
        new()
        {
            Id = "sense-voice-multi",
            DisplayName = "SenseVoice — Multi (~230 MB)",
            Family = "SenseVoice",
            Engine = ModelEngine.SherpaOnnx,
            SherpaType = SherpaModelType.SenseVoice,
            Files =
            [
                new("model.int8.onnx", $"{SenseVoiceRepo}/model.int8.onnx"),
                new("tokens.txt", $"{SenseVoiceRepo}/tokens.txt"),
            ],
            ApproxSizeMb = 230,
            Languages = ["zh", "en", "ja", "ko", "yue"]
        },
    ];

    public static ModelDefinition? GetById(string? id)
        => All.FirstOrDefault(m => m.Id == id);

    public static ModelDefinition GetByIdOrDefault(string? id)
        => GetById(id) ?? All[1]; // default to whisper-base
}
