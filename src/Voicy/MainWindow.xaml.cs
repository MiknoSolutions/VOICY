using System.ComponentModel;
using Hardcodet.Wpf.TaskbarNotification;
using System.Windows;
using System.Windows.Controls;
using Voicy.Models;
using Voicy.ViewModels;

namespace Voicy;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;
    private readonly TaskbarIcon _trayIcon;
    private bool _forceClose;

    public MainWindow(MainViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
        _trayIcon.TrayMouseDoubleClick += TrayIcon_TrayMouseDoubleClick;
        viewModel.Start();
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        if (!_forceClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        ViewModel.Dispose();
        _trayIcon.Dispose();
    }

    private void MinimizeToTray_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void TrayShow_Click(object sender, RoutedEventArgs e)
    {
        ShowWindow();
    }

    private void TraySettings_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.OpenSettingsCommand.CanExecute(null))
        {
            ShowWindow();
            ViewModel.OpenSettingsCommand.Execute(null);
        }
    }

    private void TrayIcon_TrayMouseDoubleClick(object? sender, RoutedEventArgs e)
    {
        ShowWindow();
    }

    private void TrayExit_Click(object sender, RoutedEventArgs e)
    {
        _forceClose = true;
        Close();
    }

    private void Mode_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tag)
        {
            if (Enum.TryParse<RecognitionMode>(tag, out var mode))
            {
                ViewModel.CurrentMode = mode;
            }
        }
    }

    public void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }
}