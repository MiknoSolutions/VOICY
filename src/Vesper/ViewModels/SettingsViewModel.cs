using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Vesper.Models;
using Vesper.Services;
using Vesper.Services.Audio;

namespace Vesper.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IModelDownloadService _modelDownload;
    private readonly IAudioCaptureService _audioCapture;
    private AppSettings _settings;

    public SettingsViewModel(
        ISettingsService settingsService,
        IModelDownloadService modelDownload,
        IAudioCaptureService audioCapture)
    {
        _settingsService = settingsService;
        _modelDownload = modelDownload;
        _audioCapture = audioCapture;
        _settings = _settingsService.Load();
        _settings.MigrateIfNeeded();

        AvailableModels = new ObservableCollection<ModelDefinition>(ModelCatalog.All);
        AvailableDevices = new ObservableCollection<string>(_audioCapture.GetAvailableDevices());
        Languages = new ObservableCollection<LanguageOption>(GetLanguages());

        LoadFromSettings();

        SaveCommand = new RelayCommand(Save);
        DownloadModelCommand = new RelayCommand(DownloadModel, () => !IsDownloading);
    }

    // ── Properties ──

    private WhisperBackend _backend;
    public WhisperBackend Backend
    {
        get => _backend;
        set
        {
            if (SetProperty(ref _backend, value))
            {
                OnPropertyChanged(nameof(IsApiMode));
                OnPropertyChanged(nameof(IsLocalApiMode));
            }
        }
    }
    public bool IsApiMode => Backend == WhisperBackend.Api;
    public bool IsLocalApiMode => Backend == WhisperBackend.LocalApi;

    private string _apiKey = string.Empty;
    public string ApiKey
    {
        get => _apiKey;
        set => SetProperty(ref _apiKey, value);
    }

    private ModelDefinition _selectedModel = ModelCatalog.GetByIdOrDefault(null);
    public ModelDefinition SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (value != null && SetProperty(ref _selectedModel, value))
            {
                OnPropertyChanged(nameof(IsModelDownloaded));
                OnPropertyChanged(nameof(SelectedModelInfo));
            }
        }
    }

    private string _selectedLanguage = "auto";
    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set => SetProperty(ref _selectedLanguage, value);
    }

    private int _selectedDeviceIndex;
    public int SelectedDeviceIndex
    {
        get => _selectedDeviceIndex;
        set => SetProperty(ref _selectedDeviceIndex, value);
    }

    private RecognitionMode _mode;
    public RecognitionMode Mode
    {
        get => _mode;
        set => SetProperty(ref _mode, value);
    }

    // Hotkey
    private string _hotkeyDisplay = "Ctrl+Shift+R";
    public string HotkeyDisplay
    {
        get => _hotkeyDisplay;
        set => SetProperty(ref _hotkeyDisplay, value);
    }

    private int _hotkeyModifiers = 0x02 | 0x04;
    private int _hotkeyKey = 0x52;
    private bool _isCapturingHotkey;
    public bool IsCapturingHotkey
    {
        get => _isCapturingHotkey;
        set => SetProperty(ref _isCapturingHotkey, value);
    }

    // VAD — use double to match Slider.Value type (avoids TwoWay binding type mismatch)
    private double _vadThresholdDb = -30.0;
    public double VadThresholdDb
    {
        get => _vadThresholdDb;
        set => SetProperty(ref _vadThresholdDb, value);
    }

    private double _vadSilenceMs = 800.0;
    public double VadSilenceMs
    {
        get => _vadSilenceMs;
        set => SetProperty(ref _vadSilenceMs, value);
    }

    // Download
    private bool _isDownloading;
    public bool IsDownloading
    {
        get => _isDownloading;
        private set
        {
            if (SetProperty(ref _isDownloading, value))
                DownloadModelCommand.RaiseCanExecuteChanged();
        }
    }

    private double _downloadProgress;
    public double DownloadProgress
    {
        get => _downloadProgress;
        private set => SetProperty(ref _downloadProgress, value);
    }

    private string _downloadStatus = string.Empty;
    public string DownloadStatus
    {
        get => _downloadStatus;
        private set => SetProperty(ref _downloadStatus, value);
    }

    public bool IsModelDownloaded => _modelDownload.IsModelDownloaded(SelectedModel);

    public string SelectedModelInfo
    {
        get
        {
            var m = SelectedModel;
            var langs = string.Join(", ", m.Languages).ToUpperInvariant();
            var engine = m.Engine == ModelEngine.WhisperNet ? "Whisper.net" : "SherpaOnnx";
            var status = m.IsDownloaded() ? "Downloaded \u2713" : "Not downloaded";
            return $"Engine: {engine} | Languages: {langs} | {status}";
        }
    }

    // Local API
    private string _localApiUrl = "http://localhost:8000";
    public string LocalApiUrl
    {
        get => _localApiUrl;
        set => SetProperty(ref _localApiUrl, value);
    }

    private string _localApiModelName = string.Empty;
    public string LocalApiModelName
    {
        get => _localApiModelName;
        set => SetProperty(ref _localApiModelName, value);
    }

    // Collections
    public ObservableCollection<ModelDefinition> AvailableModels { get; }
    public ObservableCollection<string> AvailableDevices { get; }
    public ObservableCollection<LanguageOption> Languages { get; }

    // Commands
    public RelayCommand SaveCommand { get; }
    public RelayCommand DownloadModelCommand { get; }

    // ── Result ──
    public bool Saved { get; private set; }

    // ── Methods ──

    public void CaptureHotkey(KeyEventArgs e)
    {
        if (!IsCapturingHotkey) return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Ignore modifier-only presses
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
            return;

        int modifiers = 0;
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) modifiers |= 0x02;
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) modifiers |= 0x04;
        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)) modifiers |= 0x01;

        int vk = KeyInterop.VirtualKeyFromKey(key);

        _hotkeyModifiers = modifiers;
        _hotkeyKey = vk;
        HotkeyDisplay = FormatHotkey(modifiers, vk);
        IsCapturingHotkey = false;

        e.Handled = true;
    }

    private void Save()
    {
        _settings.Backend = Backend;
        _settings.ApiKey = ApiKey;
        _settings.SelectedModelId = SelectedModel.Id;
        _settings.ModelSize = SelectedModel.Id.StartsWith("whisper-")
            ? SelectedModel.Id["whisper-".Length..] : SelectedModel.Id;
        _settings.Language = SelectedLanguage;
        _settings.MicrophoneDeviceIndex = SelectedDeviceIndex;
        _settings.Mode = Mode;
        _settings.HotkeyModifiers = _hotkeyModifiers;
        _settings.HotkeyKey = _hotkeyKey;
        _settings.VadThresholdDb = (float)VadThresholdDb;
        _settings.VadSilenceMs = (int)VadSilenceMs;
        _settings.LocalApiUrl = LocalApiUrl;
        _settings.LocalApiModelName = LocalApiModelName;

        _settingsService.Save(_settings);
        Saved = true;
    }

    private async void DownloadModel()
    {
        IsDownloading = true;
        DownloadStatus = $"Downloading {SelectedModel.DisplayName}...";
        DownloadProgress = 0;

        try
        {
            var progress = new Progress<double>(p =>
            {
                DownloadProgress = p;
                DownloadStatus = $"Downloading {SelectedModel.DisplayName}... {p:F0}%";
            });

            await _modelDownload.DownloadModelAsync(SelectedModel, progress);

            DownloadStatus = "Download complete!";
            OnPropertyChanged(nameof(IsModelDownloaded));
            OnPropertyChanged(nameof(SelectedModelInfo));
        }
        catch (Exception ex)
        {
            DownloadStatus = $"Download failed: {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
        }
    }

    private void LoadFromSettings()
    {
        Backend = _settings.Backend;
        ApiKey = _settings.ApiKey;
        SelectedModel = AvailableModels.FirstOrDefault(m => m.Id == _settings.SelectedModelId)
                        ?? AvailableModels[1];
        SelectedLanguage = _settings.Language;
        SelectedDeviceIndex = _settings.MicrophoneDeviceIndex;
        Mode = _settings.Mode;
        _hotkeyModifiers = _settings.HotkeyModifiers;
        _hotkeyKey = _settings.HotkeyKey;
        HotkeyDisplay = FormatHotkey(_hotkeyModifiers, _hotkeyKey);
        VadThresholdDb = _settings.VadThresholdDb;
        VadSilenceMs = _settings.VadSilenceMs;
        LocalApiUrl = _settings.LocalApiUrl;
        LocalApiModelName = _settings.LocalApiModelName;
    }

    private static string FormatHotkey(int modifiers, int key)
    {
        var parts = new List<string>();
        if ((modifiers & 0x02) != 0) parts.Add("Ctrl");
        if ((modifiers & 0x04) != 0) parts.Add("Shift");
        if ((modifiers & 0x01) != 0) parts.Add("Alt");
        if ((modifiers & 0x08) != 0) parts.Add("Win");

        var keyName = KeyInterop.KeyFromVirtualKey(key).ToString();
        parts.Add(keyName);

        return string.Join("+", parts);
    }

    private static List<LanguageOption> GetLanguages()
    {
        return
        [
            new("auto", "Auto-detect"),
            new("en", "English"),
            new("pl", "Polish"),
            new("de", "German"),
            new("fr", "French"),
            new("es", "Spanish"),
            new("it", "Italian"),
            new("pt", "Portuguese"),
            new("nl", "Dutch"),
            new("ja", "Japanese"),
            new("zh", "Chinese"),
            new("ko", "Korean"),
            new("ru", "Russian"),
            new("uk", "Ukrainian"),
            new("cs", "Czech"),
            new("sv", "Swedish"),
        ];
    }
}

public record LanguageOption(string Code, string Name)
{
    public override string ToString() => Name;
}
