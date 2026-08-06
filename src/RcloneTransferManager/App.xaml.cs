using System.Windows;
using System.Windows.Threading;
using RcloneTransferManager.Services;

namespace RcloneTransferManager;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppPaths.Ensure();
        if (!File.Exists(AppPaths.RcloneExecutable))
        {
            System.Windows.MessageBox.Show(
                AppPaths.MissingRcloneMessage,
                "Required component missing",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }
        if (!AppPaths.TryDeleteLegacyJobsFile(out var cleanupError))
        {
            try { new LogService().Write("Cleanup", cleanupError); } catch { }
            System.Windows.MessageBox.Show(
                cleanupError,
                "Cleanup warning",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        base.OnStartup(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try { new LogService().Write("Application", $"Unhandled UI error: {e.Exception}"); } catch { }
        System.Windows.MessageBox.Show("The application encountered an unexpected error. A diagnostic was written to the Logs folder.", "Unexpected error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
