using System.Diagnostics;
using System.Text;
using System.Text.Json;
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

    public string RemoteName(LocationKind kind) => kind == LocationKind.GoogleDrive
        ? "google"
        : throw new ArgumentOutOfRangeException(nameof(kind));

    public bool HasRemote(string name)
    {
        if (!File.Exists(AppPaths.ConfigFile)) return false;
        return File.ReadAllLines(AppPaths.ConfigFile).Any(line => line.Trim().Equals($"[{name}]", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<string?> GetGoogleAccountIdentityAsync(CancellationToken cancellationToken = default, bool forceRefresh = false)
    {
        var cached = ReadCachedGoogleAccountIdentity();
        if (!HasRemote("google"))
        {
            ClearGoogleAccountIdentity();
            return null;
        }
        if (!forceRefresh && !string.IsNullOrWhiteSpace(cached)) return cached;

        try
        {
            var result = await _runner.RunAsync(new[]
            {
                "lsjson", "google:", "--max-depth", "1", "--metadata",
                "--drive-auth-owner-only", "--drive-metadata-owner", "read", "--log-level", "ERROR"
            }, AppPaths.ConfigFile, null, cancellationToken);

            if (result.ExitCode != 0) return cached;
            var identity = ExtractGoogleOwner(result.Lines);
            if (string.IsNullOrWhiteSpace(identity)) return cached;
            SaveGoogleAccountIdentity(identity);
            return identity;
        }
        catch (OperationCanceledException) { throw; }
        catch { return cached; }
    }

    private static string? ReadCachedGoogleAccountIdentity()
    {
        try
        {
            if (!File.Exists(AppPaths.GoogleAccountIdentityFile)) return null;
            var value = File.ReadAllText(AppPaths.GoogleAccountIdentityFile).Trim();
            return IsLikelyEmail(value) ? value : null;
        }
        catch { return null; }
    }

    private static string? ExtractGoogleOwner(IReadOnlyList<string> lines)
    {
        try
        {
            using var document = JsonDocument.Parse(string.Join(Environment.NewLine, lines));
            if (document.RootElement.ValueKind != JsonValueKind.Array) return null;
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("Metadata", out var metadata) || metadata.ValueKind != JsonValueKind.Object) continue;
                if (!metadata.TryGetProperty("owner", out var owner)) continue;
                var value = owner.GetString()?.Trim();
                if (IsLikelyEmail(value)) return value;
            }
        }
        catch (JsonException) { }
        return null;
    }

    private static bool IsLikelyEmail(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Contains('@') && value.IndexOf('@') > 0;

    private static void SaveGoogleAccountIdentity(string identity)
    {
        try { File.WriteAllText(AppPaths.GoogleAccountIdentityFile, identity.Trim()); }
        catch { }
    }

    private static void ClearGoogleAccountIdentity()
    {
        try { if (File.Exists(AppPaths.GoogleAccountIdentityFile)) File.Delete(AppPaths.GoogleAccountIdentityFile); }
        catch { }
        try { if (File.Exists(AppPaths.GoogleFolderNamesFile)) File.Delete(AppPaths.GoogleFolderNamesFile); }
        catch { }
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
        ClearGoogleAccountIdentity();
        const string type = "drive";
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
            ClearGoogleAccountIdentity();
            return true;
        }
        var result = await _runner.RunAsync(new[] { "config", "disconnect", $"{name}:" }, AppPaths.ConfigFile, null, cancellationToken);
        var disconnected = result.ExitCode == 0;
        if (disconnected)
        {
            ForgetConnection(kind);
            ClearGoogleAccountIdentity();
        }
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
        var sourceRemote = PrepareRemote(source);
        var destinationRemote = PrepareRemote(destination);
        return new PreparedConfig(AppPaths.ConfigFile, sourceRemote, destinationRemote);
    }

    private static string PrepareRemote(ResolvedLocation location)
    {
        if (!location.IsCloud || string.IsNullOrWhiteSpace(location.RootFolderId)) return location.RemoteName;
        return $"{location.RemoteName},root_folder_id={location.RootFolderId}";
    }

    public void Cleanup(PreparedConfig config) { }

    public sealed record PreparedConfig(string Path, string SourceRemote, string DestinationRemote);
}
