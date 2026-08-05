using System.Diagnostics;
using System.Text;
using RcloneTransferManager.Models;

namespace RcloneTransferManager.Services;

public sealed class RcloneProcessRunner
{
    private readonly object _gate = new();
    private Process? _currentProcess;

    public async Task<RcloneRunResult> RunAsync(IReadOnlyList<string> arguments, string configPath, Action<string>? onLine, CancellationToken cancellationToken)
    {
        if (!File.Exists(AppPaths.RcloneExecutable)) throw new FileNotFoundException("The bundled rclone.exe was not found.", AppPaths.RcloneExecutable);

        var startInfo = new ProcessStartInfo(AppPaths.RcloneExecutable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = AppPaths.Root
        };
        startInfo.ArgumentList.Add("--config");
        startInfo.ArgumentList.Add(configPath);
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var lines = new List<string>();
        void Capture(string? line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            lock (lines) lines.Add(line);
            onLine?.Invoke(line);
        }
        process.OutputDataReceived += (_, e) => Capture(e.Data);
        process.ErrorDataReceived += (_, e) => Capture(e.Data);
        if (!process.Start()) throw new InvalidOperationException("Unable to start rclone.");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        lock (_gate) _currentProcess = process;

        var cancelled = false;
        try
        {
            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
                KillCurrent();
                await process.WaitForExitAsync();
            }
            process.WaitForExit();
        }
        finally
        {
            lock (_gate) _currentProcess = null;
        }
        return new RcloneRunResult(process.ExitCode, cancelled, lines.ToList());
    }

    public void KillCurrent()
    {
        lock (_gate)
        {
            try
            {
                if (_currentProcess is { HasExited: false }) _currentProcess.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }
    }
}
