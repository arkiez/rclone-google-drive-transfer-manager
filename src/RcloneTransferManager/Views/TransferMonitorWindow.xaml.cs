using System.ComponentModel;
using System.Windows;
using RcloneTransferManager.Models;
using RcloneTransferManager.Services;

namespace RcloneTransferManager.Views;

public partial class TransferMonitorWindow : Window
{
    private readonly TransferService _service;
    private readonly TransferRequest _request;
    private CancellationTokenSource? _runCts;
    private TaskCompletionSource<bool>? _resumeSignal;
    private bool _pauseRequested;
    private bool _cancelRequested;
    private bool _running;
    private bool _started;

    public bool WasSuccessful { get; private set; }
    public bool WasCancelled { get; private set; }

    public TransferMonitorWindow(TransferService service, TransferRequest request)
    {
        _service = service;
        _request = request;
        InitializeComponent();
        JobText.Text = $"Job: {request.Job.Name}";
        RouteText.Text = $"{request.Job.Source}  →  {request.Job.Destination}";
        ModeText.Text = request.Job.Mode.ToString().ToUpperInvariant();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_started) return;
        _started = true;
        await RunLoopAsync();
    }

    private async Task RunLoopAsync()
    {
        while (true)
        {
            _runCts = new CancellationTokenSource();
            _running = true;
            _pauseRequested = false;
            SetRunningState();
            try
            {
                var result = await _service.RunAsync(
                    _request,
                    progress => Dispatcher.BeginInvoke(new Action(() => ApplyProgress(progress))),
                    line => Dispatcher.BeginInvoke(new Action(() => AppendLog(line))),
                    _runCts.Token);
                _running = false;
                _runCts.Dispose();
                _runCts = null;

                if (result.Cancelled && _pauseRequested && !_cancelRequested)
                {
                    SetPausedState();
                    _resumeSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var resume = await _resumeSignal.Task;
                    _resumeSignal = null;
                    if (resume && !_cancelRequested)
                    {
                        _pauseRequested = false;
                        continue;
                    }
                }

                WasCancelled = _cancelRequested || result.Cancelled;
                WasSuccessful = !WasCancelled && result.ExitCode == 0;
                CompleteState(result);
                return;
            }
            catch (Exception ex)
            {
                _running = false;
                _runCts?.Dispose();
                _runCts = null;
                WasSuccessful = false;
                WasCancelled = _cancelRequested;
                StatusText.Text = WasCancelled ? "Transfer cancelled" : "Transfer failed";
                DetailText.Text = ex.Message;
                AppendLog($"ERROR: {ex.Message}");
                SetCompletedButtons();
                return;
            }
        }
    }

    private void ApplyProgress(ProgressInfo progress)
    {
        if (progress.Percent is double percent)
        {
            TransferProgress.IsIndeterminate = false;
            TransferProgress.Value = Math.Clamp(percent, 0, 100);
            PercentText.Text = $"{percent:0}%";
        }
        if (!string.IsNullOrWhiteSpace(progress.Transferred)) TransferredText.Text = progress.Transferred;
        if (!string.IsNullOrWhiteSpace(progress.CurrentFile)) CurrentFileText.Text = progress.CurrentFile;
        var eta = string.Join("  ", new[] { progress.Eta, progress.Rate }.Where(v => !string.IsNullOrWhiteSpace(v)));
        if (eta.Length > 0) EtaText.Text = eta;
    }

    private void AppendLog(string line)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => AppendLog(line)));
            return;
        }
        LogList.Items.Add(line);
        while (LogList.Items.Count > 600) LogList.Items.RemoveAt(0);
        LogList.ScrollIntoView(LogList.Items[^1]);
    }

    private void SetRunningState()
    {
        StatusText.Text = "Transferring...";
        DetailText.Text = "The transfer runs in the background. You can pause or cancel it.";
        PauseButton.IsEnabled = true;
        ResumeButton.IsEnabled = false;
        CancelButton.IsEnabled = true;
        CloseButton.IsEnabled = false;
    }

    private void SetPausedState()
    {
        StatusText.Text = "Paused";
        DetailText.Text = "The current process has stopped. Resume to continue safely.";
        PauseButton.IsEnabled = false;
        ResumeButton.IsEnabled = true;
        CancelButton.IsEnabled = true;
        CloseButton.IsEnabled = false;
    }

    private void SetCompletedButtons()
    {
        PauseButton.IsEnabled = false;
        ResumeButton.IsEnabled = false;
        CancelButton.IsEnabled = false;
        CloseButton.IsEnabled = true;
    }

    private void CompleteState(RcloneRunResult result)
    {
        if (WasSuccessful)
        {
            StatusText.Text = "Transfer completed";
            DetailText.Text = "The transfer finished successfully. The full log was saved beside the app.";
        }
        else if (WasCancelled)
        {
            StatusText.Text = "Transfer cancelled";
            DetailText.Text = "No further files will be processed. You can close this window.";
        }
        else
        {
            StatusText.Text = "Transfer failed";
            DetailText.Text = result.Lines.LastOrDefault(line =>
                line.Contains("ERROR", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Failed", StringComparison.OrdinalIgnoreCase)) ?? "Check the saved log for details.";
        }
        SetCompletedButtons();
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        if (!_running || _runCts is null) return;
        _pauseRequested = true;
        StatusText.Text = "Pausing...";
        PauseButton.IsEnabled = false;
        _runCts.Cancel();
    }

    private void Resume_Click(object sender, RoutedEventArgs e)
    {
        if (_resumeSignal is not null) _resumeSignal.TrySetResult(true);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _cancelRequested = true;
        _pauseRequested = false;
        StatusText.Text = "Cancelling...";
        PauseButton.IsEnabled = false;
        ResumeButton.IsEnabled = false;
        CancelButton.IsEnabled = false;
        _runCts?.Cancel();
        _resumeSignal?.TrySetResult(false);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_running || _resumeSignal is not null)
        {
            e.Cancel = true;
            Cancel_Click(this, new RoutedEventArgs());
        }
    }
}
