using System.Diagnostics;
using System.Text;
using RcloneTransferManager.Models;

namespace RcloneTransferManager.Services;

public sealed class RcloneConfigService
{
    private readonly RcloneProcessRunner _runner;
    private readonly LogService _log;
    private readonly HashSet<LocationKind> _verifiedConnections = new();
    private readonly object _connectionCacheLock = new();

    public RcloneConfigService(RcloneProcessRunner runner, LogService log)
    {
        _runner = runner;
        _log = log;
        AppPaths.Ensure();
        if (!File.Exists(AppPaths.ConfigFile)) File.WriteAllText(AppPaths.ConfigFile, string.Empty);
    }

    public string RemoteName(LocationKind kind) => kind switch
    {
        LocationKind.GoogleDrive => "google",
        LocationKind.OneDrive => "onedrive",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public bool HasRemote(string name)
    {
        if (!File.Exists(AppPaths.ConfigFile)) return false;
        return File.ReadAllLines(AppPaths.ConfigFile).Any(line => line.Trim().Equals($"[{name}]", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> IsConnectedAsync(LocationKind kind, CancellationToken cancellationToken = default, bool forceRefresh = false)
    {
        var name = RemoteName(kind);
        if (!HasRemote(name))
        {
            ForgetConnection(kind);
            return false;
        }

        if (!forceRefresh && IsConnectionCached(kind)) return true;

        var result = await _runner.RunAsync(new[] { "lsd", $"{name}:", "--max-depth", "1", "--retries", "1", "--low-level-retries", "1", "--timeout", "20s", "--log-level", "ERROR" }, AppPaths.ConfigFile, null, cancellationToken);
        var connected = result.ExitCode == 0;
        if (connected) RememberConnection(kind); else ForgetConnection(kind);
        return connected;
    }

    public async Task<bool> ConnectAsync(LocationKind kind, Action<string>? status, CancellationToken cancellationToken = default)
    {
        AppPaths.EnsureRcloneAvailable();
        var name = RemoteName(kind);
        var type = kind == LocationKind.GoogleDrive ? "drive" : "onedrive";
        var arguments = HasRemote(name)
            ? new[] { "config", "reconnect", $"{name}:" }
            : new[] { "config", "create", name, type, "config_is_local", "true" };

        status?.Invoke($"Opening {kind} authorization in your browser...");
        var startInfo = new ProcessStartInfo(AppPaths.RcloneExecutable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = AppPaths.Root
        };
        startInfo.ArgumentList.Add("--config");
        startInfo.ArgumentList.Add(AppPaths.ConfigFile);
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        var output = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) { output.AppendLine(e.Data); status?.Invoke(e.Data); } };
        process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) { output.AppendLine(e.Data); status?.Invoke(e.Data); } };
        if (!process.Start())
        {
            ForgetConnection(kind);
            return false;
        }
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Most provider prompts have safe defaults. Newlines accept those defaults while rclone handles OAuth in the browser.
        try
        {
            await process.StandardInput.WriteAsync(string.Concat(Enumerable.Repeat(Environment.NewLine, 32)));
            await process.StandardInput.FlushAsync();
        }
        catch (InvalidOperationException) { }

        try { await process.WaitForExitAsync(cancellationToken); }
        catch (OperationCanceledException) { try { process.Kill(true); } catch { } ForgetConnection(kind); return false; }
        process.WaitForExit();
        var success = process.ExitCode == 0 && HasRemote(name);
        if (success) RememberConnection(kind); else ForgetConnection(kind);
        _log.Write("Authentication", $"{kind} connection {(success ? "completed" : "failed")}");
        return success;
    }

    public async Task<bool> DisconnectAsync(LocationKind kind, CancellationToken cancellationToken = default)
    {
        var name = RemoteName(kind);
        if (!HasRemote(name))
        {
            ForgetConnection(kind);
            return true;
        }
        var result = await _runner.RunAsync(new[] { "config", "disconnect", $"{name}:" }, AppPaths.ConfigFile, null, cancellationToken);
        var disconnected = result.ExitCode == 0;
        if (disconnected) ForgetConnection(kind);
        return disconnected;
    }

    private bool IsConnectionCached(LocationKind kind)
    {
        lock (_connectionCacheLock) return _verifiedConnections.Contains(kind);
    }

    private void RememberConnection(LocationKind kind)
    {
        lock (_connectionCacheLock) _verifiedConnections.Add(kind);
    }

    private void ForgetConnection(LocationKind kind)
    {
        lock (_connectionCacheLock) _verifiedConnections.Remove(kind);
    }

    public PreparedConfig PrepareConfig(ResolvedLocation source, ResolvedLocation destination, Guid jobId)
    {
        var path = Path.Combine(AppPaths.Data, $"run-{jobId:N}.conf");
        File.Copy(AppPaths.ConfigFile, path, true);
        var sourceRemote = PrepareRemote(path, source, "source", jobId);
        var destinationRemote = PrepareRemote(path, destination, "destination", jobId);
        return new PreparedConfig(path, sourceRemote, destinationRemote);
    }

    private static string PrepareRemote(string path, ResolvedLocation location, string side, Guid jobId)
    {
        if (!location.IsCloud || string.IsNullOrWhiteSpace(location.RootFolderId)) return location.RemoteName;
        var cloneName = $"{location.RemoteName}-{side}-{jobId:N}";
        CloneSection(path, location.RemoteName, cloneName, location.RootFolderId);
        return cloneName;
    }

    private static void CloneSection(string path, string originalName, string cloneName, string rootFolderId)
    {
        var lines = File.ReadAllLines(path).ToList();
        var start = lines.FindIndex(line => line.Trim().Equals($"[{originalName}]", StringComparison.OrdinalIgnoreCase));
        if (start < 0) throw new InvalidOperationException($"The rclone remote '{originalName}' is not configured.");
        var end = start + 1;
        while (end < lines.Count && !lines[end].TrimStart().StartsWith("[")) end++;
        var section = lines.GetRange(start, end - start);
        section[0] = $"[{cloneName}]";
        section.RemoveAll(line => line.TrimStart().StartsWith("root_folder_id", StringComparison.OrdinalIgnoreCase));
        section.Add($"root_folder_id = {rootFolderId}");
        lines.AddRange(new[] { string.Empty });
        lines.AddRange(section);
        File.WriteAllLines(path, lines);
    }

    public void Cleanup(PreparedConfig config)
    {
        try { File.Delete(config.Path); } catch { }
    }

    public sealed record PreparedConfig(string Path, string SourceRemote, string DestinationRemote);
}
