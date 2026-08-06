using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RcloneTransferManager.Models;

public enum TransferMode { Copy, Sync }
public enum LocationKind { Local, GoogleDrive, OneDrive, Remote, PublicFile }
public enum ConflictDecision { Overwrite, Skip }

public sealed record ResolvedLocation(
    LocationKind Kind,
    string Original,
    string RemoteName,
    string Path,
    string? RootFolderId,
    string? DirectUrl = null)
{
    public bool IsCloud => Kind is LocationKind.GoogleDrive or LocationKind.OneDrive or LocationKind.Remote;
    public bool IsPublicFile => Kind == LocationKind.PublicFile;
    public string DisplayProvider => Kind switch
    {
        LocationKind.Local => "Local folder",
        LocationKind.GoogleDrive => "Google Drive",
        LocationKind.OneDrive => "OneDrive",
        LocationKind.PublicFile => "Public file",
        _ => "rclone remote"
    };
}

public sealed class TransferJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Transfer";
    public string Source { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public TransferMode Mode { get; set; } = TransferMode.Copy;
}

public sealed record TransferRequest(TransferJob Job, IReadOnlyCollection<string> ExcludedPaths);
public sealed record ProgressInfo(double? Percent, string? Transferred, string? Rate, string? Eta, string? CurrentFile, string RawLine);
public sealed record SyncChange(string Action, string Path);

public sealed class ConflictItem : INotifyPropertyChanged
{
    private ConflictDecision _decision = ConflictDecision.Overwrite;
    public ConflictItem(string path) => Path = path;
    public string Path { get; }
    public ConflictDecision Decision { get => _decision; set { if (_decision == value) return; _decision = value; OnPropertyChanged(); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed record PreviewResult(bool Succeeded, IReadOnlyList<SyncChange> Changes, IReadOnlyList<string> Lines, string? Error);
public sealed record RcloneRunResult(int ExitCode, bool Cancelled, IReadOnlyList<string> Lines);
