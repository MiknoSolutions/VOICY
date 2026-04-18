using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Voicy.Models;
using Voicy.Services;
using Voicy.Services.Audio;
using Voicy.Services.Input;
using Voicy.Services.Recognition;

namespace Voicy.ViewModels;

public class MainViewModel : ViewModelBase, IDisposable
{
    private readonly IAudioCaptureService _audio;
    private readonly IGlobalHotkeyService _hotkey;
    private readonly ITextInjectionService _textInjection;
    private readonly IVoiceActivityDetector _vad;
    private readonly ISettingsService _settingsService;
    private readonly LocalWhisperService _localWhisper;
    private readonly OpenAiWhisperService _apiWhisper;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dispatcher _dispatcher;

    private AppSettings _settings;
    private bool _isToggleActive;
    private CancellationTokenSource? _cts;

    public MainViewModel(
        IAudioCaptureService audio,
        IGlobalHotkeyService hotkey,
        ITextInjectionService textInjection,
        IVoiceActivityDetector vad,
        ISettingsService settingsService,
        LocalWhisperService localWhisper,
        OpenAiWhisperService apiWhisper,
        IServiceProvider serviceProvider)
    {
        _audio = audio;
        _hotkey = hotkey;
        _textInjection = textInjection;
        _vad = vad;
        _settingsService = settingsService;
        _localWhisper = localWhisper;
        _apiWhisper = apiWhisper;
        _serviceProvider = serviceProvider;
        _dispatcher = Application.Current.Dispatcher;

        _settings = _settingsService.Load();
        ApplySettings();

        OpenSettingsCommand = new RelayCommand(OpenSettings);
        ShowWindowCommand = new RelayCommand(ShowWindow);

        _hotkey.HotkeyToggled += OnHotkeyToggled;
        _hotkey.HotkeyPressed += OnHotkeyPressed;
        _hotkey.HotkeyReleased += OnHotkeyReleased;
        _vad.SpeechEnded += OnSpeechEnded;
    }

    // ── Properties ──

    private RecognitionMode _currentMode;
    public RecognitionMode CurrentMode
    {
        get => _currentMode;
        set
        {
            if (SetProperty(ref _currentMode, value))
            {
                OnPropertyChanged(nameof(IsToggleMode));
                OnPropertyChanged(nameof(IsPushToTalkMode));
                OnPropertyChanged(nameof(IsContinuousMode));
                StopContinuousMode();
                if (value == RecognitionMode.Continuous)
                    StartContinuousMode();
            }
        }
    }

    private bool _isListening;
    public bool IsListening
    {
        get => _isListening;
        private set => SetProperty(ref _isListening, value);
    }

    private bool _isProcessing;
    public bool IsProcessing
    {
        get => _isProcessing;
        private set => SetProperty(ref _isProcessing, value);
    }

    private string _statusText = "Ready";
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    private string _lastTranscription = string.Empty;
    public string LastTranscription
    {
        get => _lastTranscription;
        private set => SetProperty(ref _lastTranscription, value);
    }

    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand ShowWindowCommand { get; }

    // Mode booleans for RadioButton binding
    public bool IsToggleMode
    {
        get => CurrentMode == RecognitionMode.Toggle;
        set { if (value) CurrentMode = RecognitionMode.Toggle; }
    }

    public bool IsPushToTalkMode
    {
        get => CurrentMode == RecognitionMode.PushToTalk;
        set { if (value) CurrentMode = RecognitionMode.PushToTalk; }
    }

    public bool IsContinuousMode
    {
        get => CurrentMode == RecognitionMode.Continuous;
        set { if (value) CurrentMode = RecognitionMode.Continuous; }
    }

    private string _hotkeyDisplay = "Ctrl+Shift+R";
    public string HotkeyDisplay
    {
        get => _hotkeyDisplay;
        private set => SetProperty(ref _hotkeyDisplay, value);
    }

    // ── Public Methods ──

    public void Start()
    {
        _hotkey.Start();
        if (CurrentMode == RecognitionMode.Continuous)
            StartContinuousMode();
        StatusText = "Ready — press hotkey to start";
    }

    public void ReloadSettings()
    {
        _settings = _settingsService.Load();
        ApplySettings();
    }

    private void OpenSettings()
    {
        var settingsVm = new SettingsViewModel(
            _settingsService,
            (IModelDownloadService)_serviceProvider.GetService(typeof(IModelDownloadService))!,
            _audio);

        var settingsWindow = new Views.SettingsWindow(settingsVm);
        settingsWindow.Owner = Application.Current.MainWindow;

        if (settingsWindow.ShowDialog() == true)
        {
            ReloadSettings();
        }
    }

    private void ShowWindow()
    {
        var mainWindow = Application.Current.MainWindow;
        if (mainWindow != null)
        {
            mainWindow.Show();
            mainWindow.WindowState = WindowState.Normal;
            mainWindow.Activate();
        }
    }

    // ── Hotkey Handlers ──

    private void OnHotkeyToggled(object? sender, EventArgs e)
    {
        if (CurrentMode != RecognitionMode.Toggle) return;

        _dispatcher.Invoke(() =>
        {
            if (!_isToggleActive)
            {
                _isToggleActive = true;
                StartRecording();
            }
            else
            {
                _isToggleActive = false;
                _ = StopAndTranscribeAsync();
            }
        });
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        if (CurrentMode != RecognitionMode.PushToTalk) return;

        _dispatcher.Invoke(() => StartRecording());
    }

    private void OnHotkeyReleased(object? sender, EventArgs e)
    {
        if (CurrentMode != RecognitionMode.PushToTalk) return;

        _dispatcher.Invoke(() => _ = StopAndTranscribeAsync());
    }

    // ── Continuous Mode ──

    private void StartContinuousMode()
    {
        _vad.ThresholdDb = _settings.VadThresholdDb;
        _vad.SilenceDurationMs = _settings.VadSilenceMs;
        _vad.MinSpeechDurationMs = _settings.VadMinSpeechMs;
        _vad.Reset();

        _audio.DataAvailable += OnAudioDataForVad;
        _audio.StartRecording();
        IsListening = true;
        StatusText = "Continuous — listening...";
    }

    private void StopContinuousMode()
    {
        _audio.DataAvailable -= OnAudioDataForVad;
        if (IsListening && CurrentMode == RecognitionMode.Continuous)
        {
            _audio.StopRecording();
            IsListening = false;
        }
        _vad.Reset();
    }

    private void OnAudioDataForVad(object? sender, float[] samples)
    {
        _vad.ProcessAudioFrame(samples);
    }

    private void OnSpeechEnded(object? sender, float[] speechSamples)
    {
        // Convert float32 samples back to WAV bytes
        var wavBytes = ConvertFloat32ToWav(speechSamples);
        _dispatcher.Invoke(() => _ = TranscribeAndInjectAsync(wavBytes));
    }

    // ── Recording ──

    private void StartRecording()
    {
        _audio.StartRecording();
        IsListening = true;
        StatusText = "Listening...";
    }

    private async Task StopAndTranscribeAsync()
    {
        var wav = _audio.StopRecording();
        IsListening = false;

        if (wav.Length == 0)
        {
            StatusText = "No audio captured";
            return;
        }

        await TranscribeAndInjectAsync(wav);
    }

    private async Task TranscribeAndInjectAsync(byte[] wavAudio)
    {
        IsProcessing = true;
        StatusText = "Processing...";

        try
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            var recognizer = GetRecognizer();
            var text = await Task.Run(
                () => recognizer.TranscribeAsync(wavAudio, _settings.Language, _cts.Token),
                _cts.Token);

            if (!string.IsNullOrWhiteSpace(text))
            {
                LastTranscription = text;
                _textInjection.InjectText(text);
                StatusText = "Done — text pasted";
            }
            else
            {
                StatusText = "No speech detected";
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    // ── Helpers ──

    private ISpeechRecognitionService GetRecognizer()
    {
        return _settings.Backend == WhisperBackend.Local
            ? _localWhisper
            : _apiWhisper;
    }

    private void ApplySettings()
    {
        CurrentMode = _settings.Mode;
        _hotkey.SetHotkey(_settings.HotkeyModifiers, _settings.HotkeyKey);
        _audio.SetDevice(_settings.MicrophoneDeviceIndex);
        HotkeyDisplay = FormatHotkey(_settings.HotkeyModifiers, _settings.HotkeyKey);

        if (_settings.Backend == WhisperBackend.Local)
        {
            if (System.IO.File.Exists(_settings.ModelFilePath))
                _localWhisper.LoadModel(_settings.ModelFilePath);
        }
        else
        {
            _apiWhisper.SetApiKey(_settings.ApiKey);
        }
    }

    private static string FormatHotkey(int modifiers, int key)
    {
        var parts = new List<string>();
        if ((modifiers & 0x02) != 0) parts.Add("Ctrl");
        if ((modifiers & 0x04) != 0) parts.Add("Shift");
        if ((modifiers & 0x01) != 0) parts.Add("Alt");
        if ((modifiers & 0x08) != 0) parts.Add("Win");
        var keyName = System.Windows.Input.KeyInterop.KeyFromVirtualKey(key).ToString();
        parts.Add(keyName);
        return string.Join("+", parts);
    }

    private static byte[] ConvertFloat32ToWav(float[] samples)
    {
        const int sampleRate = 16000;
        const int bitsPerSample = 16;
        const int channels = 1;

        using var ms = new System.IO.MemoryStream();
        using var writer = new System.IO.BinaryWriter(ms);

        int dataLength = samples.Length * 2; // 16-bit samples
        int fileLength = 36 + dataLength;

        // WAV header
        writer.Write("RIFF"u8);
        writer.Write(fileLength);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16); // chunk size
        writer.Write((short)1); // PCM
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bitsPerSample / 8); // byte rate
        writer.Write((short)(channels * bitsPerSample / 8)); // block align
        writer.Write((short)bitsPerSample);
        writer.Write("data"u8);
        writer.Write(dataLength);

        foreach (var sample in samples)
        {
            var clamped = Math.Clamp(sample, -1f, 1f);
            writer.Write((short)(clamped * 32767));
        }

        return ms.ToArray();
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        StopContinuousMode();
        _hotkey.HotkeyToggled -= OnHotkeyToggled;
        _hotkey.HotkeyPressed -= OnHotkeyPressed;
        _hotkey.HotkeyReleased -= OnHotkeyReleased;
        _vad.SpeechEnded -= OnSpeechEnded;
        _hotkey.Stop();
    }
}
