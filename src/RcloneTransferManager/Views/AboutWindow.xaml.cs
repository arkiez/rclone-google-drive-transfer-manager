using System.Windows;
using RcloneTransferManager.Services;

namespace RcloneTransferManager.Views;

public partial class AboutWindow : Window
{
    private readonly UpdateService _updates;

    public AboutWindow(UpdateService updates)
    {
        InitializeComponent();
        _updates = updates;
        VersionText.Text = $"Version {AppInfo.Version}  |  Created by {AppInfo.Creator}";
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false;
        UpdateStatusText.Text = "Checking GitHub Releases...";
        try
        {
            var update = await _updates.CheckLatestAsync(force: true);
            if (update is null)
            {
                UpdateStatusText.Text = "You're up to date.";
                return;
            }
            UpdateStatusText.Text = $"Version {update.Version} is available.";
            new UpdateWindow(_updates, update) { Owner = this }.ShowDialog();
        }
        catch (Exception ex)
        {
            UpdateStatusText.Text = "Could not check for updates.";
            MessageBox.Show(ex.Message, "Update check failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            CheckUpdatesButton.IsEnabled = true;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
