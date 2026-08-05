using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using RcloneTransferManager.Models;
using RcloneTransferManager.Services;
using RcloneTransferManager.Views;

namespace RcloneTransferManager;

public partial class MainWindow : Window
{
    private readonly RcloneProcessRunner _runner = new();
    private readonly LogService _log = new();
    private readonly JobStore _jobStore = new();
    private readonly RcloneConfigService _config;
    private readonly TransferService _transferService;
    private List<TransferJob> _jobs = new();
    private bool _loadingJob;

    public MainWindow()
    {
        InitializeComponent();
        _config = new RcloneConfigService(_runner, _log);
        _transferService = new TransferService(_runner, _config, _log);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e) => await LoadJobsAsync();

    private async Task LoadJobsAsync()
    {
        try
        {
            _jobs = await _jobStore.LoadAsync();
            SavedJobCombo.ItemsSource = null;
            SavedJobCombo.ItemsSource = _jobs;
            SavedJobCombo.DisplayMemberPath = nameof(TransferJob.Name);
        }
        catch (Exception ex) { ValidationText.Text = $"Could not load saved jobs: {ex.Message}"; }
    }

    private void RefreshJobs_Click(object sender, RoutedEventArgs e) => _ = LoadJobsAsync();

    private void SavedJobCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingJob || SavedJobCombo.SelectedItem is not TransferJob job) return;
        _loadingJob = true;
        JobNameBox.Text = job.Name; SourceBox.Text = job.Source; DestinationBox.Text = job.Destination; CopyRadio.IsChecked = job.Mode == TransferMode.Copy; SyncRadio.IsChecked = job.Mode == TransferMode.Sync;
        ValidationText.Text = $"Loaded '{job.Name}'. Review the locations, then start when ready.";
        _loadingJob = false;
    }

    private void LocationBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateLocationStatus(SourceBox, SourceStatus);
        UpdateLocationStatus(DestinationBox, DestinationStatus);
    }

    private void UpdateLocationStatus(System.Windows.Controls.TextBox box, TextBlock status)
    {
        if (string.IsNullOrWhiteSpace(box.Text)) { status.Text = ""; status.Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush"); return; }
        if (!LocationResolver.TryResolve(box.Text, out var location, out var error)) { status.Text = error; status.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush"); return; }
        if (location!.IsPublicFile)
        {
            status.Text = "Public file - No login required";
            status.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");
            return;
        }
        var connected = location!.IsCloud && _config.HasRemote(location.RemoteName);
        status.Text = location.IsCloud ? $"{location.DisplayProvider} · {(connected ? "Connected" : "Login required")}" : "Local folder · Browse or paste a path";
        status.Foreground = (System.Windows.Media.Brush)FindResource(connected || !location.IsCloud ? "SuccessBrush" : "WarningBrush");
    }

    private void BrowseSource_Click(object sender, RoutedEventArgs e) => BrowseInto(SourceBox);
    private void BrowseDestination_Click(object sender, RoutedEventArgs e) => BrowseInto(DestinationBox);

    private static void BrowseInto(System.Windows.Controls.TextBox target)
    {
        var dialog = new OpenFolderDialog { Title = "Choose a local folder", Multiselect = false };
        if (dialog.ShowDialog() == true) target.Text = dialog.FolderName;
    }

    private async void SaveJob_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBuildJob(out var job, out var error)) { ValidationText.Text = error; return; }
        _jobs.Add(job!);
        await _jobStore.SaveAsync(_jobs);
        await LoadJobsAsync();
        ValidationText.Text = $"Saved '{job!.Name}'.";
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

            UpdateLocationStatus(SourceBox, SourceStatus);
            UpdateLocationStatus(DestinationBox, DestinationStatus);
            if (!_transferService.TryResolveJob(job!, out source, out destination, out error))
            {
                ValidationText.Text = error;
                System.Windows.MessageBox.Show(error, "Cannot start transfer", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IReadOnlyCollection<string> excluded = Array.Empty<string>();
            if (job!.Mode == TransferMode.Sync)
            {
                ValidationText.Text = "Preparing Sync preview...";
                var preview = await _transferService.PreviewAsync(job, line => Dispatcher.Invoke(() => ValidationText.Text = line));
                if (!preview.Succeeded) { ShowFailure(preview.Error ?? "Could not prepare Sync preview."); return; }
                var previewWindow = new SyncPreviewWindow(preview.Changes, preview.Lines) { Owner = this };
                if (previewWindow.ShowDialog() != true) { ValidationText.Text = "Sync cancelled before any changes were applied."; return; }
            }
            else if (source!.IsPublicFile)
            {
                ValidationText.Text = "Preparing public file download...";
            }
            else
            {
                var conflicts = await _transferService.FindCopyConflictsAsync(job);
                if (conflicts.Count > 0)
                {
                    var conflictWindow = new ConflictWindow(conflicts) { Owner = this };
                    if (conflictWindow.ShowDialog() != true) { ValidationText.Text = "Copy cancelled before conflicts were resolved."; return; }
                    excluded = conflicts.Where(c => c.Decision == ConflictDecision.Skip).Select(c => c.Path).ToList();
                }
            }

            var monitor = new TransferMonitorWindow(_transferService, new TransferRequest(job!, excluded)) { Owner = this };
            monitor.ShowDialog();
            job!.LastRunUtc = DateTime.UtcNow;
            job.LastRunStatus = monitor.WasSuccessful ? "Completed" : monitor.WasCancelled ? "Cancelled" : "Failed";
            await _jobStore.SaveAsync(_jobs);
            ValidationText.Text = $"{job.LastRunStatus}: {job.Name}";
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
        LocationKind.OneDrive => "OneDrive",
        _ => "the cloud provider"
    };

    private bool TryBuildJob(out TransferJob? job, out string error)
    {
        job = null; error = string.Empty;
        var name = JobNameBox.Text.Trim();
        if (name.Length == 0) { error = "Enter a job name."; return false; }
        if (string.IsNullOrWhiteSpace(SourceBox.Text) || string.IsNullOrWhiteSpace(DestinationBox.Text)) { error = "Enter both a source and destination."; return false; }
        job = new TransferJob { Name = name, Source = SourceBox.Text.Trim(), Destination = DestinationBox.Text.Trim(), Mode = SyncRadio.IsChecked == true ? TransferMode.Sync : TransferMode.Copy };
        return true;
    }

    private void Accounts_Click(object sender, RoutedEventArgs e) => new AccountsWindow(_config) { Owner = this }.ShowDialog();

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var rclone = "bundled rclone";
        try { rclone = FileVersionInfo.GetVersionInfo(AppPaths.RcloneExecutable).FileVersion ?? rclone; } catch { }
        System.Windows.MessageBox.Show($"{AppInfo.Name} v{AppInfo.Version}\n\nCreated by {AppInfo.Creator}\nEngine: rclone {rclone}\n\nPortable Windows transfer utility.", "About", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ShowFailure(string message) => System.Windows.MessageBox.Show(message, "Transfer Manager", MessageBoxButton.OK, MessageBoxImage.Error);
}
