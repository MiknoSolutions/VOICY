using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Vesper.Services;
using Vesper.Services.Audio;
using Vesper.Services.Input;
using Vesper.Services.Recognition;
using Vesper.ViewModels;

namespace Vesper;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Start diagnostic log
        DiagnosticLogger.Clear();
        DiagnosticLogger.LogEnvironment();

        // Global exception handlers to prevent silent crashes
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        try
        {
            var mainVm = _serviceProvider.GetRequiredService<MainViewModel>();
            var mainWindow = new MainWindow(mainVm);
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            // Unwrap DI/reflection wrappers to show the real cause
            var inner = ex;
            while (inner.InnerException != null) inner = inner.InnerException;

            MessageBox.Show(
                $"Failed to start VESPER:\n\n{inner.Message}\n\nThe application will start with default settings.",
                "VESPER — Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            // Reset settings to defaults and retry
            try
            {
                var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
                settingsService.Save(new Models.AppSettings());

                // Rebuild services with clean settings
                _serviceProvider.Dispose();
                services = new ServiceCollection();
                ConfigureServices(services);
                _serviceProvider = services.BuildServiceProvider();

                var mainVm = _serviceProvider.GetRequiredService<MainViewModel>();
                var mainWindow = new MainWindow(mainVm);
                MainWindow = mainWindow;
                mainWindow.Show();
            }
            catch (Exception ex2)
            {
                MessageBox.Show(
                    $"VESPER cannot start:\n\n{ex2.Message}",
                    "VESPER — Fatal Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
            }
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}\n\n{e.Exception.StackTrace}",
            "VESPER — Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            MessageBox.Show(
                $"A fatal error occurred:\n\n{ex.Message}",
                "VESPER — Fatal Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
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
        services.AddSingleton<LocalApiService>();
        services.AddSingleton<SherpaOnnxService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
