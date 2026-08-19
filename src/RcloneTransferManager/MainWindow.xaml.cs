using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Microsoft.Win32;
using RcloneTransferManager.Models;
using RcloneTransferManager.Services;
using RcloneTransferManager.Views;

namespace RcloneTransferManager;

public partial class MainWindow : Window
{
    private const string CloudSourcePrompt = "Paste a Google Drive file or folder link, or a public direct file link";
    private const string CloudDestinationPrompt = "Paste a Google Drive folder link";
    private const string LocalPrompt = "Enter a local folder path or click Browse";

    private readonly RcloneProcessRunner _runner = new();
    private readonly LogService _log = new();
    private readonly RcloneConfigService _config;
    private readonly TransferService _transferService;
    private readonly UpdateService _updateService = new();

    public MainWindow()
    {
        InitializeComponent();
        Title = $"{AppInfo.Name} v{AppInfo.Version}";
        VersionText.Text = $"v{AppInfo.Version}  |  {AppInfo.Creator}";
        _config = new RcloneConfigService(_runner, _log);
        _transferService = new TransferService(_runner, _config, _log);
        DestinationCloudRadio.Checked += DestinationLocationMode_Checked;
        DestinationLocalRadio.Checked += DestinationLocationMode_Checked;
        ConfigureLocationInput(SourceBox, SourcePlaceholder, null, SourceStatus, cloudMode: true, isSource: true, clearInput: false);
        ConfigureLocationInput(DestinationBox, DestinationPlaceholder, BrowseDestinationButton, DestinationStatus, cloudMode: false, isSource: false, clearInput: false);
        Loaded += async (_, _) => await CheckForUpdatesOnStartupAsync();
    }

    private async Task CheckForUpdatesOnStartupAsync()
    {
        try
        {
            var update = await _updateService.CheckLatestAsync(force: false);
            if (update is not null)
                new UpdateWindow(_updateService, update) { Owner = this }.ShowDialog();
        }
        catch (Exception ex)
        {
            _log.Write("Update", ex.Message);
        }
    }

    private void LocationBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateWatermark(SourceBox, SourcePlaceholder);
        UpdateWatermark(DestinationBox, DestinationPlaceholder);
        UpdateClearButton(SourceBox, SourceClearButton);
        UpdateClearButton(DestinationBox, DestinationClearButton);
        UpdateLocationStatus(SourceBox, SourceStatus, cloudMode: true, isSource: true);
        UpdateLocationStatus(DestinationBox, DestinationStatus, DestinationCloudRadio.IsChecked == true, isSource: false);
    }

    private void DestinationLocationMode_Checked(object sender, RoutedEventArgs e) =>
        ConfigureLocationInput(DestinationBox, DestinationPlaceholder, BrowseDestinationButton, DestinationStatus, DestinationCloudRadio.IsChecked == true, isSource: false, clearInput: true);

    private void ConfigureLocationInput(
        System.Windows.Controls.TextBox box,
        TextBlock placeholder,
        Button? browseButton,
        TextBlock status,
        bool cloudMode,
        bool isSource,
        bool clearInput)
    {
        if (clearInput) box.Clear();
        var prompt = cloudMode
            ? isSource ? CloudSourcePrompt : CloudDestinationPrompt
            : LocalPrompt;
        placeholder.Text = prompt;
        if (browseButton is not null) browseButton.Visibility = cloudMode ? Visibility.Collapsed : Visibility.Visible;
        AutomationProperties.SetName(box, $"{(isSource ? "Source" : "Destination")} {(cloudMode ? "cloud link" : "local folder")}");
        AutomationProperties.SetHelpText(box, prompt);
        box.ToolTip = prompt;
        status.Text = string.Empty;
        status.Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush");
        UpdateWatermark(box, placeholder);
    }

    private static void UpdateWatermark(System.Windows.Controls.TextBox box, TextBlock placeholder) =>
        placeholder.Visibility = string.IsNullOrEmpty(box.Text) ? Visibility.Visible : Visibility.Collapsed;

    private static void UpdateClearButton(System.Windows.Controls.TextBox box, Button button) =>
        button.Visibility = string.IsNullOrWhiteSpace(box.Text) ? Visibility.Collapsed : Visibility.Visible;

    private void ClearSource_Click(object sender, RoutedEventArgs e)
    {
        SourceBox.Clear();
        SourceBox.Focus();
    }

    private void ClearDestination_Click(object sender, RoutedEventArgs e)
    {
        DestinationBox.Clear();
        DestinationBox.Focus();
    }

    private void UpdateLocationStatus(System.Windows.Controls.TextBox box, TextBlock status, bool cloudMode, bool isSource)
    {
        if (string.IsNullOrWhiteSpace(box.Text)) { status.Text = ""; status.Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush"); return; }
        if (!LocationResolver.TryResolve(box.Text, out var location, out var error)) { status.Text = error; status.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush"); return; }
        if (!MatchesSelectedLocationType(location!, cloudMode, isSource))
        {
            status.Text = GetLocationTypeError(isSource, cloudMode);
            status.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
            return;
        }
        if (location!.IsPublicFile)
        {
            status.Text = "Public file - No login required";
            status.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");
            return;
        }
        var connected = location!.IsCloud && _config.HasRemote(location.RemoteName);
        status.Text = location.IsCloud ? $"{location.DisplayProvider} - {(connected ? "Connected" : "Login required")}" : "Local folder - Ready";
        status.Foreground = (System.Windows.Media.Brush)FindResource(connected || !location.IsCloud ? "SuccessBrush" : "WarningBrush");
    }

    private static bool MatchesSelectedLocationType(ResolvedLocation location, bool cloudMode, bool isSource) =>
        cloudMode
            ? location.IsCloud || (isSource && location.IsPublicFile)
            : location.Kind == LocationKind.Local;

    private static string GetLocationTypeError(bool isSource, bool cloudMode)
    {
        if (isSource) return "Source must be a supported Google Drive file/folder link or public direct file link.";
        return cloudMode
            ? "Destination must be a supported cloud folder link when Cloud is selected."
            : "Destination must be a local folder path when Local is selected.";
    }

    private void BrowseDestination_Click(object sender, RoutedEventArgs e) => BrowseInto(DestinationBox);

    private static void BrowseInto(System.Windows.Controls.TextBox target)
    {
        var dialog = new OpenFolderDialog { Title = "Choose a local folder", Multiselect = false };
        if (dialog.ShowDialog() == true) target.Text = dialog.FolderName;
    }

    private async void StartTransfer_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBuildJob(out var job, out var error)) { ValidationText.Text = error; return; }

        IsEnabled = false;
        try
        {
            ResolvedLocation? source;
            ResolvedLocation? destination;
            if (!_transferService.TryResolveJob(job!, out source, out destination, out error, requireConnections: false, createDestinationDirectory: false))
            {
                ValidationText.Text = error;
                System.Windows.MessageBox.Show(error, "Cannot start transfer", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var connection = await EnsureCloudConnectionsAsync(source!, destination!);
            if (!connection.Succeeded)
            {
                ValidationText.Text = connection.Error;
                System.Windows.MessageBox.Show(connection.Error, "Login required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            UpdateLocationStatus(SourceBox, SourceStatus, cloudMode: true, isSource: true);
            UpdateLocationStatus(DestinationBox, DestinationStatus, DestinationCloudRadio.IsChecked == true, isSource: false);
            if (!_transferService.TryResolveJob(job!, out source, out destination, out error))
            {
                ValidationText.Text = error;
                System.Windows.MessageBox.Show(error, "Cannot start transfer", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IReadOnlyCollection<string> excluded = Array.Empty<string>();
            if (source!.IsPublicFile)
            {
                ValidationText.Text = "Preparing public file download...";
            }
            else
            {
                var conflicts = await _transferService.FindCopyConflictsAsync(job!);
                if (conflicts.Count > 0)
                {
                    var conflictWindow = new ConflictWindow(conflicts) { Owner = this };
                    if (conflictWindow.ShowDialog() != true) { ValidationText.Text = "Copy cancelled before conflicts were resolved."; return; }
                    excluded = conflicts.Where(c => c.Decision == ConflictDecision.Skip).Select(c => c.Path).ToList();
                }
            }

            var monitor = new TransferMonitorWindow(_transferService, new TransferRequest(job!, excluded)) { Owner = this };
            monitor.ShowDialog();
            ValidationText.Text = monitor.WasSuccessful
                ? "Transfer completed."
                : monitor.WasCancelled ? "Transfer cancelled." : "Transfer failed.";
        }
        catch (Exception ex) { _log.Write("Transfer", ex.ToString()); ShowFailure(ex.Message); }
        finally { IsEnabled = true; }
    }

    private async Task<(bool Succeeded, string Error)> EnsureCloudConnectionsAsync(ResolvedLocation source, ResolvedLocation destination)
    {
        var required = new[] { source, destination }
            .Where(location => location.IsCloud)
            .Select(location => location.Kind)
            .Distinct()
            .ToList();
        if (required.Count == 0) return (true, string.Empty);

        var missing = new List<LocationKind>();
        foreach (var kind in required)
        {
            try
            {
                if (!await _config.IsConnectedAsync(kind)) missing.Add(kind);
            }
            catch (Exception ex)
            {
                return (false, $"Could not check {ProviderName(kind)} connection: {ex.Message}");
            }
        }

        if (missing.Count == 0) return (true, string.Empty);

        ValidationText.Text = $"Login required for {string.Join(" and ", missing.Select(ProviderName))}. Opening Accounts...";
        var accounts = new AccountsWindow(_config, missing) { Owner = this };
        accounts.ShowDialog();

        var failed = new List<LocationKind>();
        foreach (var kind in missing)
        {
            try
            {
                if (!await _config.IsConnectedAsync(kind)) failed.Add(kind);
            }
            catch { failed.Add(kind); }
        }

        return failed.Count == 0
            ? (true, string.Empty)
            : (false, $"Login is required for {string.Join(" and ", failed.Distinct().Select(ProviderName))} before this transfer can start.");
    }

    private static string ProviderName(LocationKind kind) => kind switch
    {
        LocationKind.GoogleDrive => "Google Drive",
        _ => "the cloud provider"
    };

    private bool TryBuildJob(out TransferJob? job, out string error)
    {
        job = null; error = string.Empty;
        var source = SourceBox.Text.Trim();
        var destination = DestinationBox.Text.Trim();
        if (source.Length == 0 || destination.Length == 0) { error = "Enter both a source and destination."; return false; }
        if (!TryValidateSelectedLocation(source, cloudMode: true, isSource: true, out error)) return false;
        if (!TryValidateSelectedLocation(destination, DestinationCloudRadio.IsChecked == true, isSource: false, out error)) return false;
        job = new TransferJob
        {
            Name = $"Transfer {DateTime.Now:yyyyMMdd-HHmmss}",
            Source = source,
            Destination = destination,
            Mode = TransferMode.Copy
        };
        return true;
    }

    private static bool TryValidateSelectedLocation(string value, bool cloudMode, bool isSource, out string error)
    {
        if (!LocationResolver.TryResolve(value, out var location, out error)) return false;
        if (MatchesSelectedLocationType(location!, cloudMode, isSource)) return true;
        error = GetLocationTypeError(isSource, cloudMode);
        return false;
    }

    private void Accounts_Click(object sender, RoutedEventArgs e) => new AccountsWindow(_config) { Owner = this }.ShowDialog();

    private void About_Click(object sender, RoutedEventArgs e) =>
        new AboutWindow(_updateService) { Owner = this }.ShowDialog();

    private void ShowFailure(string message) => System.Windows.MessageBox.Show(message, "Transfer Manager", MessageBoxButton.OK, MessageBoxImage.Error);
}
