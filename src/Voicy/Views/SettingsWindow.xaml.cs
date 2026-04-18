using System.Windows;
using System.Windows.Input;
using Voicy.ViewModels;

namespace Voicy.Views;

public partial class SettingsWindow : Window
{
    private SettingsViewModel ViewModel => (SettingsViewModel)DataContext;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();

        // Set PasswordBox value (can't bind directly)
        ApiKeyBox.Password = viewModel.ApiKey;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        ViewModel.CaptureHotkey(e);
    }

    private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        ViewModel.ApiKey = ApiKeyBox.Password;
    }

    private void CaptureHotkey_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.IsCapturingHotkey = !ViewModel.IsCapturingHotkey;
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
}
