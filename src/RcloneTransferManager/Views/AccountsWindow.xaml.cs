using System.ComponentModel;
using System.Windows;
using RcloneTransferManager.Models;
using RcloneTransferManager.Services;
using WpfButton = System.Windows.Controls.Button;
using WpfTextBlock = System.Windows.Controls.TextBlock;

namespace RcloneTransferManager.Views;

public partial class AccountsWindow : Window
{
    private readonly RcloneConfigService _config;
    private readonly CancellationTokenSource _windowCts = new();
    private readonly IReadOnlyList<LocationKind> _autoConnectKinds;
    private bool _busy;
    private bool _googleConnected;
    private bool _oneDriveConnected;

    public AccountsWindow(RcloneConfigService config, IEnumerable<LocationKind>? autoConnectKinds = null)
    {
        _config = config;
        _autoConnectKinds = (autoConnectKinds ?? Array.Empty<LocationKind>())
            .Where(kind => kind is LocationKind.GoogleDrive or LocationKind.OneDrive)
            .Distinct()
            .ToArray();
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        DoneButton.IsEnabled = false;
        await RefreshAsync();
        foreach (var kind in _autoConnectKinds)
        {
            if (!IsConnected(kind)) await ConnectProviderAsync(kind);
        }
        DoneButton.IsEnabled = true;
    }

    private async Task RefreshAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            GoogleStatus.Text = "Checking connection...";
            OneDriveStatus.Text = "Checking connection...";
            var googleTask = _config.IsConnectedAsync(LocationKind.GoogleDrive, _windowCts.Token, forceRefresh: true);
            var oneDriveTask = _config.IsConnectedAsync(LocationKind.OneDrive, _windowCts.Token, forceRefresh: true);
            var states = await Task.WhenAll(googleTask, oneDriveTask);
            _googleConnected = states[0];
            _oneDriveConnected = states[1];
            SetState(LocationKind.GoogleDrive, states[0], GoogleStatus, GoogleConnectButton, GoogleDisconnectButton);
            SetState(LocationKind.OneDrive, states[1], OneDriveStatus, OneDriveConnectButton, OneDriveDisconnectButton);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            GoogleStatus.Text = $"Could not check accounts: {ex.Message}";
            OneDriveStatus.Text = "Try Refresh again.";
        }
        finally { _busy = false; }
    }

    private async void GoogleConnect_Click(object sender, RoutedEventArgs e) =>
        await ConnectAsync(LocationKind.GoogleDrive, GoogleStatus, GoogleConnectButton, GoogleDisconnectButton);

    private async void OneDriveConnect_Click(object sender, RoutedEventArgs e) =>
        await ConnectAsync(LocationKind.OneDrive, OneDriveStatus, OneDriveConnectButton, OneDriveDisconnectButton);

    private Task ConnectProviderAsync(LocationKind kind) => kind switch
    {
        LocationKind.GoogleDrive => ConnectAsync(LocationKind.GoogleDrive, GoogleStatus, GoogleConnectButton, GoogleDisconnectButton),
        LocationKind.OneDrive => ConnectAsync(LocationKind.OneDrive, OneDriveStatus, OneDriveConnectButton, OneDriveDisconnectButton),
        _ => Task.CompletedTask
    };

    private bool IsConnected(LocationKind kind) => kind switch
    {
        LocationKind.GoogleDrive => _googleConnected,
        LocationKind.OneDrive => _oneDriveConnected,
        _ => false
    };

    private async Task ConnectAsync(LocationKind kind, WpfTextBlock status, WpfButton connect, WpfButton disconnect)
    {
        SetBusy(connect, disconnect, status, "Opening browser authorization...");
        try
        {
            var ok = await _config.ConnectAsync(kind, line => SetStatusFromWorker(status, line), _windowCts.Token);
            SetState(kind, ok, status, connect, disconnect);
            if (!ok) status.Text = "Authorization did not complete. Check the log and try again.";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            status.Text = $"Connection failed: {ex.Message}";
            connect.IsEnabled = true;
            disconnect.IsEnabled = false;
        }
    }

    private async void GoogleDisconnect_Click(object sender, RoutedEventArgs e) =>
        await DisconnectAsync(LocationKind.GoogleDrive, GoogleStatus, GoogleConnectButton, GoogleDisconnectButton);

    private async void OneDriveDisconnect_Click(object sender, RoutedEventArgs e) =>
        await DisconnectAsync(LocationKind.OneDrive, OneDriveStatus, OneDriveConnectButton, OneDriveDisconnectButton);

    private async Task DisconnectAsync(LocationKind kind, WpfTextBlock status, WpfButton connect, WpfButton disconnect)
    {
        SetBusy(connect, disconnect, status, "Disconnecting...");
        try
        {
            var ok = await _config.DisconnectAsync(kind, _windowCts.Token);
            SetState(kind, !ok ? true : false, status, connect, disconnect);
            status.Text = ok ? "Not connected" : "Could not disconnect. Try again.";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            status.Text = $"Disconnect failed: {ex.Message}";
            connect.IsEnabled = true;
            disconnect.IsEnabled = true;
        }
    }

    private void SetState(LocationKind kind, bool connected, WpfTextBlock status, WpfButton connect, WpfButton disconnect)
    {
        if (kind == LocationKind.GoogleDrive) _googleConnected = connected;
        if (kind == LocationKind.OneDrive) _oneDriveConnected = connected;
        status.Text = connected ? "Connected and ready to use." : "Not connected — click Connect to authorize.";
        status.Foreground = (System.Windows.Media.Brush)FindResource(connected ? "SuccessBrush" : "MutedBrush");
        connect.IsEnabled = !connected;
        disconnect.IsEnabled = connected;
    }

    private void SetBusy(WpfButton connect, WpfButton disconnect, WpfTextBlock status, string message)
    {
        connect.IsEnabled = false;
        disconnect.IsEnabled = false;
        status.Text = message;
    }

    private void SetStatusFromWorker(WpfTextBlock target, string message)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => SetStatusFromWorker(target, message)));
            return;
        }
        target.Text = message.Length > 180 ? message[..180] + "..." : message;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();
    private void Done_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _windowCts.Cancel();
        _windowCts.Dispose();
    }
}
