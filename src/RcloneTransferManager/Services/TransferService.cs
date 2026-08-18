using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;
using RcloneTransferManager.Models;

namespace RcloneTransferManager.Services;

public sealed class TransferService
{
    private static readonly Regex PercentRegex = new(@"(?<percent>\d{1,3})%", RegexOptions.Compiled);
    private static readonly Regex ProgressRegex = new(@"Transferred:\s*(?<done>[^,]+)(?:\s*/\s*(?<total>[^,]+))?,\s*(?<percent>\d{1,3})%", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly HttpClient PublicFileClient = new(new HttpClientHandler { AllowAutoRedirect = true })
    {
        Timeout = TimeSpan.FromSeconds(30)
    };
    private readonly RcloneProcessRunner _runner;
    private readonly RcloneConfigService _config;
    private readonly LogService _log;
    private readonly GoogleDriveFolderNameService _googleFolderNames;

    public TransferService(RcloneProcessRunner runner, RcloneConfigService config, LogService log)
    {
        _runner = runner;
        _config = config;
        _log = log;
        _googleFolderNames = new GoogleDriveFolderNameService(runner);
    }

    public bool TryResolveJob(
        TransferJob job,
        out ResolvedLocation? source,
        out ResolvedLocation? destination,
        out string error,
        bool requireConnections = true,
        bool createDestinationDirectory = true)
    {
        source = null; destination = null; error = string.Empty;
        if (!LocationResolver.TryResolve(job.Source, out source, out error)) return false;
        if (!LocationResolver.TryResolve(job.Destination, out destination, out error)) return false;
        if (source is null || destination is null) { error = "Could not resolve the source or destination."; return false; }
        if (source.Original.Equals(destination.Original, StringComparison.OrdinalIgnoreCase)) { error = "Source and destination must be different."; return false; }
        if (destination.IsPublicFile) { error = "A public direct file link can only be used as a source."; return false; }
        if (source.IsPublicFile && destination.Kind != LocationKind.Local) { error = "Public direct file links can only be copied to a local folder."; return false; }
        if (source.IsPublicFile && string.IsNullOrWhiteSpace(source.DirectUrl)) { error = "This public file link does not contain a usable download URL."; return false; }
        if (requireConnections && source.IsCloud && !_config.HasRemote(source.RemoteName)) { error = $"Connect {source.DisplayProvider} before starting this transfer."; return false; }
        if (requireConnections && destination.IsCloud && !_config.HasRemote(destination.RemoteName)) { error = $"Connect {destination.DisplayProvider} before starting this transfer."; return false; }
        if (source.Kind == LocationKind.Local && !Directory.Exists(source.Path)) { error = "The local source folder does not exist."; return false; }
        if (createDestinationDirectory && destination.Kind == LocationKind.Local) Directory.CreateDirectory(destination.Path);
        return true;
    }

    public async Task<PreviewResult> PreviewAsync(TransferJob job, Action<string>? onLine, CancellationToken cancellationToken = default)
    {
        if (!TryResolveJob(job, out var source, out var destination, out var error)) return new(false, Array.Empty<SyncChange>(), Array.Empty<string>(), error);
        if (source!.IsPublicFile)
            return new(false, Array.Empty<SyncChange>(), Array.Empty<string>(), "Public direct file sources do not support a folder preview.");
        RcloneConfigService.PreparedConfig? prepared = null;
        try
        {
            var effectiveDestination = await ResolveFolderCopyDestinationAsync(source!, destination!, cancellationToken);
            prepared = _config.PrepareConfig(source!, effectiveDestination, job.Id);
            var args = BuildArguments(source!, effectiveDestination, prepared, dryRun: true, Array.Empty<string>());
            var result = await _runner.RunAsync(args, prepared.Path, onLine, cancellationToken);
            var changes = ParseChanges(result.Lines);
            return new(result.ExitCode == 0, changes, result.Lines, result.ExitCode == 0 ? null : GetFriendlyError(result.Lines));
        }
        catch (Exception ex) { return new(false, Array.Empty<SyncChange>(), Array.Empty<string>(), ex.Message); }
        finally { if (prepared is not null) _config.Cleanup(prepared); }
    }

    public async Task<IReadOnlyList<ConflictItem>> FindCopyConflictsAsync(TransferJob job, CancellationToken cancellationToken = default)
    {
        var preview = await PreviewAsync(job, null, cancellationToken);
        if (!preview.Succeeded) throw new InvalidOperationException(preview.Error ?? "Could not preview Copy changes.");
        return preview.Changes.Where(c => c.Action.Equals("Update", StringComparison.OrdinalIgnoreCase)).Select(c => new ConflictItem(c.Path)).ToList();
    }

    public async Task<RcloneRunResult> RunAsync(TransferRequest request, Action<ProgressInfo>? onProgress, Action<string>? onLine, CancellationToken cancellationToken = default)
    {
        if (!TryResolveJob(request.Job, out var source, out var destination, out var error)) throw new InvalidOperationException(error);
        var effectiveDestination = source!.IsPublicFile
            ? destination!
            : await ResolveFolderCopyDestinationAsync(source, destination!, cancellationToken);
        var logPath = _log.CreateTransferLog(request.Job.Name, GetRcloneVersion());
        void Capture(string line)
        {
            _log.WriteFile(logPath, line);
            onLine?.Invoke(line);
            onProgress?.Invoke(ParseProgress(line));
        }
        _log.WriteFile(logPath, $"Source: {DescribeLocation(source!)}");
        _log.WriteFile(logPath, $"Destination: {DescribeLocation(effectiveDestination)}");
        _log.WriteFile(logPath, $"Mode: {request.Job.Mode}");

        if (source!.IsPublicFile)
        {
            var destinationFile = await ResolvePublicFileDestinationAsync(source.DirectUrl!, destination!.Path, cancellationToken);
            var publicArgs = BuildPublicFileArguments(source.DirectUrl!, destinationFile);
            return await _runner.RunAsync(publicArgs, AppPaths.ConfigFile, Capture, cancellationToken);
        }

        RcloneConfigService.PreparedConfig? prepared = null;
        try
        {
            prepared = _config.PrepareConfig(source, effectiveDestination, request.Job.Id);
            var args = BuildArguments(source, effectiveDestination, prepared, false, request.ExcludedPaths);
            return await _runner.RunAsync(args, prepared.Path, Capture, cancellationToken);
        }
        finally { if (prepared is not null) _config.Cleanup(prepared); }
    }

    private async Task<ResolvedLocation> ResolveFolderCopyDestinationAsync(ResolvedLocation source, ResolvedLocation destination, CancellationToken cancellationToken)
    {
        string folderName;
        if (source.Kind == LocationKind.GoogleDrive
            && string.IsNullOrWhiteSpace(source.Path)
            && !string.IsNullOrWhiteSpace(source.RootFolderId))
        {
            folderName = await _googleFolderNames.GetFolderNameAsync(source.RootFolderId, cancellationToken)
                ?? throw new InvalidOperationException("Could not determine the Google Drive source folder name. Refresh the connection and try again.");
        }
        else folderName = GetSourceFolderName(source);

        return AppendFolderToDestination(destination, folderName);
    }

    public static string GetSourceFolderName(ResolvedLocation source)
    {
        if (source.Kind == LocationKind.Local)
        {
            var trimmed = source.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var name = Path.GetFileName(trimmed);
            if (!string.IsNullOrWhiteSpace(name)) return name;
        }

        var normalized = source.Path.Replace('\\', '/').Trim('/');
        if (!string.IsNullOrWhiteSpace(normalized)) return normalized.Split('/').Last();
        throw new InvalidOperationException("Could not determine the source folder name.");
    }

    public static ResolvedLocation AppendFolderToDestination(ResolvedLocation destination, string folderName)
    {
        var segment = SanitizeFolderSegment(folderName, destination.Kind == LocationKind.Local);
        if (string.IsNullOrWhiteSpace(segment)) throw new InvalidOperationException("The source folder name is not usable at the destination.");
        var path = destination.Kind == LocationKind.Local
            ? Path.Combine(destination.Path, segment)
            : string.IsNullOrWhiteSpace(destination.Path)
                ? segment
                : $"{destination.Path.Replace('\\', '/').TrimEnd('/')}/{segment}";
        return destination with { Path = path };
    }

    public static string? ParseGoogleFolderNameResponse(string json) =>
        GoogleDriveFolderNameService.ParseFolderNameResponse(json);

    private static string SanitizeFolderSegment(string folderName, bool local)
    {
        var value = folderName.Trim();
        if (local)
        {
            foreach (var invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
        }
        else value = value.Replace('/', '／').Replace('\\', '＼');
        return value.Trim().Trim('.');
    }

    private static async Task<string> ResolvePublicFileDestinationAsync(string directUrl, string destinationFolder, CancellationToken cancellationToken)
    {
        using var response = await PublicFileClient.GetAsync(directUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"The public link returned HTTP {(int)response.StatusCode}. It may be private, expired, or require sign-in.");

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (string.Equals(mediaType, "text/html", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This link opened a web page instead of a public downloadable file. Use the provider's direct download link or sign in.");

        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            var finalUri = response.RequestMessage?.RequestUri;
            var segment = finalUri?.Segments.LastOrDefault()?.Trim('/');
            if (!string.IsNullOrWhiteSpace(segment))
            {
                var candidate = Uri.UnescapeDataString(segment);
                if (!candidate.Equals("uc", StringComparison.OrdinalIgnoreCase)
                    && !candidate.Equals("download", StringComparison.OrdinalIgnoreCase)
                    && !candidate.Equals("download.aspx", StringComparison.OrdinalIgnoreCase))
                    fileName = candidate;
            }
        }

        fileName = SanitizeFileName(fileName);
        return Path.Combine(destinationFolder, string.IsNullOrWhiteSpace(fileName) ? "public-download" : fileName);
    }

    private static string SanitizeFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var name = value.Trim().Trim('"');
        name = Path.GetFileName(name);
        foreach (var invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
        return name.Trim().Trim('.');
    }

    private static List<string> BuildPublicFileArguments(string directUrl, string destinationFile)
    {
        return new List<string>
        {
            "copyurl",
            directUrl,
            destinationFile,
            "--no-clobber",
            "--stats",
            "1s",
            "--stats-one-line",
            "--stats-one-line-date",
            "--stats-log-level",
            "NOTICE",
            "--log-level",
            "INFO",
            "--retries",
            "2",
            "--low-level-retries",
            "5"
        };
    }

    private static string DescribeLocation(ResolvedLocation location)
    {
        if (location.IsPublicFile) return "Public direct file (URL redacted)";
        if (location.IsCloud) return $"{location.DisplayProvider} (link redacted)";
        return location.Path;
    }

    private static List<string> BuildArguments(ResolvedLocation source, ResolvedLocation destination, RcloneConfigService.PreparedConfig prepared, bool dryRun, IReadOnlyCollection<string> excluded)
    {
        var args = new List<string> { "copy", ToSpec(source, prepared.SourceRemote), ToSpec(destination, prepared.DestinationRemote), "--stats", "1s", "--stats-one-line", "--stats-one-line-date", "--stats-log-level", "NOTICE", "--log-level", "INFO", "--retries", "2", "--low-level-retries", "5" };
        args.Add("--create-empty-src-dirs");
        if (dryRun)
        {
            args.Add("--dry-run");
            args.Add("--combined");
            args.Add("-");
        }
        foreach (var path in excluded.Where(p => !string.IsNullOrWhiteSpace(p))) { args.Add("--exclude"); args.Add(path.Replace('\\', '/')); }
        return args;
    }

    private static string ToSpec(ResolvedLocation location, string remote) => location.IsCloud ? $"{remote}:{location.Path}" : location.Path;

    private static IReadOnlyList<SyncChange> ParseChanges(IEnumerable<string> lines)
    {
        var changes = new List<SyncChange>();
        foreach (var line in lines)
        {
            var combined = line.Trim();
            if (combined.Length > 2 && combined[1] == ' ' && combined[0] is '+' or '*' or '-')
            {
                var combinedAction = combined[0] switch { '+' => "Add", '*' => "Update", '-' => "Delete", _ => string.Empty };
                if (combinedAction.Length > 0) changes.Add(new SyncChange(combinedAction, combined[2..].Trim()));
                continue;
            }
            var action = line.Contains("Copied (new)", StringComparison.OrdinalIgnoreCase) ? "Add" : line.Contains("Copied (replaced)", StringComparison.OrdinalIgnoreCase) ? "Update" : line.Contains("Deleted", StringComparison.OrdinalIgnoreCase) ? "Delete" : null;
            if (action is null) continue;
            var marker = action == "Add" ? "Copied (new)" : action == "Update" ? "Copied (replaced)" : "Deleted";
            var path = line[..line.IndexOf(marker, StringComparison.OrdinalIgnoreCase)].Trim().TrimEnd(':');
            if (path.Contains(':')) path = path[(path.LastIndexOf(':') + 1)..].Trim();
            changes.Add(new SyncChange(action, string.IsNullOrWhiteSpace(path) ? marker : path));
        }
        return changes;
    }

    private static ProgressInfo ParseProgress(string line)
    {
        var match = ProgressRegex.Match(line);
        if (!match.Success)
        {
            var percent = PercentRegex.Match(line);
            return new(percent.Success && double.TryParse(percent.Groups["percent"].Value, out var p) ? p : null, null, null, null, ExtractFile(line), line);
        }
        var pct = double.Parse(match.Groups["percent"].Value, CultureInfo.InvariantCulture);
        return new(pct, match.Groups["done"].Value.Trim(), null, ExtractEta(line), ExtractFile(line), line);
    }

    private static string? ExtractEta(string line)
    {
        var marker = line.IndexOf("ETA", StringComparison.OrdinalIgnoreCase);
        return marker >= 0 ? line[marker..].Trim() : null;
    }

    private static string? ExtractFile(string line)
    {
        var marker = line.IndexOf("Copied", StringComparison.OrdinalIgnoreCase);
        if (marker <= 0) return null;
        var value = line[..marker].Trim().TrimEnd(':');
        return value.Contains(':') ? value[(value.LastIndexOf(':') + 1)..].Trim() : value;
    }

    private string GetRcloneVersion()
    {
        try { return File.Exists(AppPaths.RcloneExecutable) ? FileVersionInfo.GetVersionInfo(AppPaths.RcloneExecutable).FileVersion ?? "bundled" : "missing"; }
        catch { return "bundled"; }
    }

    private static string GetFriendlyError(IReadOnlyList<string> lines)
    {
        if (lines.Any(l => l.Contains("403", StringComparison.OrdinalIgnoreCase)
            || l.Contains("401", StringComparison.OrdinalIgnoreCase)
            || l.Contains("permission denied", StringComparison.OrdinalIgnoreCase)
            || l.Contains("access denied", StringComparison.OrdinalIgnoreCase)))
            return "Access denied. Sign in with the account that can access this shared item, or ask the owner to grant access.";
        if (lines.Any(l => l.Contains("html", StringComparison.OrdinalIgnoreCase)
            || l.Contains("sign in", StringComparison.OrdinalIgnoreCase)
            || l.Contains("not a valid", StringComparison.OrdinalIgnoreCase)))
            return "This link did not return a public downloadable file. Use a direct public download link or sign in to the provider.";
        var line = lines.LastOrDefault(l => l.Contains("ERROR", StringComparison.OrdinalIgnoreCase) || l.Contains("Failed", StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(line) ? "rclone could not complete the operation." : line;
    }
}
