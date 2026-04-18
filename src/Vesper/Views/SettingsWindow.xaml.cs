using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Vesper.ViewModels;

namespace Vesper.Views;

public partial class SettingsWindow : Window
{
    private SettingsViewModel ViewModel => (SettingsViewModel)DataContext;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();

        // Set DataContext AFTER InitializeComponent so that
        // all controls (Sliders, etc.) have their Min/Max ranges
        // configured before bindings resolve with actual values.
        DataContext = viewModel;

        // Set PasswordBox value (can't bind directly)
        ApiKeyBox.Password = viewModel.ApiKey;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        ViewModel.CaptureHotkey(e);
        ViewModel.CaptureHotkey2(e);
    }

    private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        ViewModel.ApiKey = ApiKeyBox.Password;
    }

    private void CaptureHotkey_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.IsCapturingHotkey = !ViewModel.IsCapturingHotkey;
    }

    private void CaptureHotkey2_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.IsCapturingHotkey2 = !ViewModel.IsCapturingHotkey2;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Saved)
            DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void BrowseGoogleCredentials_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select Google Cloud Service Account JSON",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dlg.ShowDialog(this) == true)
        {
            try
            {
                var safePath = ViewModel.ImportCredentialsFile(dlg.FileName);
                MessageBox.Show(
                    $"Credentials imported to:\n{safePath}\n\nThe original file can be safely deleted.",
                    "Vesper", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import credentials:\n{ex.Message}",
                    "Vesper — Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
