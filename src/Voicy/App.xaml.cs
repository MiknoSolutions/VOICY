using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Voicy.Services;
using Voicy.Services.Audio;
using Voicy.Services.Input;
using Voicy.Services.Recognition;
using Voicy.ViewModels;

namespace Voicy;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        var mainVm = _serviceProvider.GetRequiredService<MainViewModel>();
        var mainWindow = new MainWindow(mainVm);
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IModelDownloadService, ModelDownloadService>();
        services.AddSingleton<IAudioCaptureService, AudioCaptureService>();
        services.AddSingleton<IVoiceActivityDetector, EnergyVoiceActivityDetector>();
        services.AddSingleton<IGlobalHotkeyService, LowLevelKeyboardHookService>();
        services.AddSingleton<ITextInjectionService, ClipboardTextInjectionService>();
        services.AddSingleton<LocalWhisperService>();
        services.AddSingleton<OpenAiWhisperService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
