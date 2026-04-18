using System.Text.Json.Serialization;

namespace Voicy.Models;

public class AppSettings
{
    public WhisperBackend Backend { get; set; } = WhisperBackend.Local;
    public string ApiKey { get; set; } = string.Empty;
    public string ModelSize { get; set; } = "base";
    public string SelectedModelId { get; set; } = "whisper-base";
    public string Language { get; set; } = "auto";
    public RecognitionMode Mode { get; set; } = RecognitionMode.Toggle;

    // Local API server settings
    public string LocalApiUrl { get; set; } = "http://localhost:8000";
    public string LocalApiModelName { get; set; } = string.Empty;

    // Hotkey for Toggle / PushToTalk modes
    public int HotkeyModifiers { get; set; } = 0x02 | 0x04; // Ctrl + Shift
    public int HotkeyKey { get; set; } = 0x52; // R key

    // Audio
    public int MicrophoneDeviceIndex { get; set; } = 0;

    // VAD (continuous mode)
    public float VadThresholdDb { get; set; } = -30f;
    public int VadSilenceMs { get; set; } = 800;
    public int VadMinSpeechMs { get; set; } = 300;

    [JsonIgnore]
    public string ModelsDirectory => System.IO.Path.Combine(AppContext.BaseDirectory, "models");

    [JsonIgnore]
    public string ModelFilePath => System.IO.Path.Combine(ModelsDirectory, $"ggml-{ModelSize}.bin");

    /// <summary>
    /// Migrate old settings: if SelectedModelId is default but ModelSize was customized, fix it.
    /// </summary>
    public void MigrateIfNeeded()
    {
        if (SelectedModelId == "whisper-base" && ModelSize != "base")
        {
            SelectedModelId = $"whisper-{ModelSize}";
        }
    }
}
