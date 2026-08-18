using System.ComponentModel;
using System.Windows;
using RcloneTransferManager.Services;

namespace RcloneTransferManager.Views;

public partial class UpdateWindow : Window
{
    private readonly UpdateService _updates;
    private readonly UpdateInfo _update;
    private bool _installing;

    public UpdateWindow(UpdateService updates, UpdateInfo update)
    {
        InitializeComponent();
        _updates = updates;
        _update = update;
        VersionSummaryText.Text = $"Current: {AppInfo.Version}    Latest: {update.Version}";
        ReleaseNotesText.Text = string.IsNullOrWhiteSpace(update.Notes) ? "No release notes provided." : update.Notes.Trim();
    }

    private async void UpdateNow_Click(object sender, RoutedEventArgs e)
    {
        _installing = true;
        UpdateNowButton.IsEnabled = false;
        LaterButton.IsEnabled = false;
        DownloadProgress.Visibility = Visibility.Visible;
        DownloadStatusText.Text = "Downloading update...";
        try
        {
            var progress = new Progress<double>(value =>
            {
                DownloadProgress.Value = value;
                DownloadStatusText.Text = $"Downloading update... {value:0}%";
            });
            var package = await _updates.DownloadAsync(_update, progress);
            DownloadStatusText.Text = "Verified. Restarting to install...";
            _updates.LaunchUpdater(_update, package);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            _installing = false;
            DownloadStatusText.Text = "Update failed.";
            MessageBox.Show(ex.Message, "Update failed", MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateNowButton.IsEnabled = true;
            LaterButton.IsEnabled = true;
        }
    }

    private void UpdateWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_installing) e.Cancel = true;
    }

    private void Later_Click(object sender, RoutedEventArgs e) => Close();
}
