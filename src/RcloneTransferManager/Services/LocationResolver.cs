using System.Text.RegularExpressions;
using RcloneTransferManager.Models;

namespace RcloneTransferManager.Services;

public static class LocationResolver
{
    private static readonly Regex GoogleFolder = new("/folders/(?<id>[A-Za-z0-9_-]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex GoogleFile = new("/file/d/(?<id>[A-Za-z0-9_-]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool TryResolve(string? input, out ResolvedLocation? location, out string error)
    {
        location = null; error = string.Empty;
        var value = input?.Trim() ?? string.Empty;
        if (value.Length == 0) { error = "Enter a link or local folder path."; return false; }
        if (LooksLikeLocalPath(value)) { location = new(LocationKind.Local, value, string.Empty, value, null); return true; }
        if (TryResolveRclonePath(value, out location)) return true;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        { error = "Use a Google Drive folder link or a valid local folder path."; return false; }

        var host = uri.Host.ToLowerInvariant();
        if (TryResolvePublicFile(value, uri, out location)) return true;
        if (host.Contains("drive.google.com") || host.Contains("docs.google.com"))
        {
            var match = GoogleFolder.Match(uri.AbsolutePath);
            var id = match.Success ? match.Groups["id"].Value : Query(uri, "id");
            if (string.IsNullOrWhiteSpace(id)) { error = "This Google Drive link does not contain a recognizable folder ID."; return false; }
            location = new(LocationKind.GoogleDrive, value, "google", string.Empty, Uri.UnescapeDataString(id)); return true;
        }
        error = "Only Google Drive links are supported."; return false;
    }

    private static bool TryResolvePublicFile(string value, Uri uri, out ResolvedLocation? location)
    {
        location = null;
        var host = uri.Host.ToLowerInvariant();

        if (host.Contains("drive.google.com"))
        {
            var fileMatch = GoogleFile.Match(uri.AbsolutePath);
            if (fileMatch.Success)
            {
                var id = fileMatch.Groups["id"].Value;
                var resourceKey = Query(uri, "resourcekey");
                var direct = $"https://drive.google.com/uc?export=download&id={Uri.EscapeDataString(id)}";
                if (!string.IsNullOrWhiteSpace(resourceKey))
                    direct += $"&resourcekey={Uri.EscapeDataString(resourceKey)}";
                location = new(LocationKind.PublicFile, value, string.Empty, string.Empty, null, direct);
                return true;
            }

            if (uri.AbsolutePath.Equals("/uc", StringComparison.OrdinalIgnoreCase)
                && string.Equals(Query(uri, "export"), "download", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(Query(uri, "id")))
            {
                location = new(LocationKind.PublicFile, value, string.Empty, string.Empty, null, value);
                return true;
            }
        }

        if (host.Contains("drive.usercontent.google.com")
            && !string.IsNullOrWhiteSpace(Query(uri, "id")))
        {
            location = new(LocationKind.PublicFile, value, string.Empty, string.Empty, null, value);
            return true;
        }


        return false;
    }

    private static bool TryResolveRclonePath(string value, out ResolvedLocation? location)
    {
        location = null;
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return false;
        var colon = value.IndexOf(':'); if (colon <= 0) return false;
        var prefix = value[..colon].ToLowerInvariant(); var path = value[(colon + 1)..].TrimStart('/', '\\');
        var kind = prefix switch { "gdrive" or "google" => LocationKind.GoogleDrive, _ => LocationKind.Remote };
        var remote = kind == LocationKind.GoogleDrive ? "google" : value[..colon];
        location = new(kind, value, remote, path, null); return true;
    }

    private static bool LooksLikeLocalPath(string value) => Regex.IsMatch(value, "^[A-Za-z]:[\\\\/]") || value.StartsWith("\\\\") || value.StartsWith(".") || value.StartsWith("/");
    private static string? Query(Uri uri, string key) => uri.Query.TrimStart('?')
        .Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(p => p.Split('=', 2))
        .Where(p => p.Length == 2 && p[0].Equals(key, StringComparison.OrdinalIgnoreCase))
        .Select(p => Uri.UnescapeDataString(p[1]))
        .FirstOrDefault();
}
