using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace RcloneTransferManager.Services;

public sealed record UpdateInfo(
    Version Version,
    string Tag,
    string Notes,
    string DownloadUrl,
    string Digest,
    long Size);

public sealed class UpdateService
{
    public const string Repository = "arkiez/rclone-google-drive-transfer-manager";
    private const string ApiUrl = "https://api.github.com/repos/" + Repository + "/releases/latest";
    private static readonly HttpClient Client = CreateClient();
    private readonly Version _currentVersion;

    public UpdateService()
    {
        _currentVersion = Version.TryParse(AppInfo.Version, out var parsed) ? parsed : new Version(0, 0, 0);
    }
    public async Task<UpdateInfo?> CheckLatestAsync(bool force, CancellationToken cancellationToken = default)
    {
        if (!force && !ShouldCheckNow()) return null;

        using var response = await Client.GetAsync(ApiUrl, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            MarkChecked();
            return null;
        }
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var latest = ParseLatestRelease(json);
        MarkChecked();
        return latest is not null && latest.Version > _currentVersion ? latest : null;
    }

    public static UpdateInfo? ParseLatestRelease(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var tag = root.TryGetProperty("tag_name", out var tagValue) ? tagValue.GetString() ?? "" : "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var version)) return null;
        var notes = root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "";
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) return null;
        var expectedName = $"RcloneTransferManager-v{version}-win-x64.zip";
        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameValue) ? nameValue.GetString() ?? "" : "";
            if (!string.Equals(name, expectedName, StringComparison.OrdinalIgnoreCase)) continue;
            var url = asset.TryGetProperty("browser_download_url", out var urlValue) ? urlValue.GetString() ?? "" : "";
            var digest = asset.TryGetProperty("digest", out var digestValue) ? digestValue.GetString() ?? "" : "";
            var size = asset.TryGetProperty("size", out var sizeValue) && sizeValue.TryGetInt64(out var n) ? n : 0;
            if (url.Length == 0 || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) return null;
            return new UpdateInfo(version, tag, notes, url, digest, size);
        }
        return null;
    }

    public async Task<string> DownloadAsync(UpdateInfo update, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var updateDir = Path.Combine(Path.GetTempPath(), "RcloneTransferManager", "updates", update.Version.ToString());
        Directory.CreateDirectory(updateDir);
        var zipPath = Path.Combine(updateDir, $"RcloneTransferManager-v{update.Version}-win-x64.zip");
        using var response = await Client.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? update.Size;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = File.Create(zipPath);
        var buffer = new byte[1024 * 128];
        long copied = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            copied += read;
            if (total > 0) progress?.Report(copied * 100d / total);
        }
        await output.FlushAsync(cancellationToken);
        output.Close();

        if (!VerifyDigest(zipPath, update.Digest))
        {
            File.Delete(zipPath);
            throw new InvalidDataException("The downloaded update failed SHA-256 verification.");
        }
        progress?.Report(100);
        return zipPath;
    }

    public static bool VerifyDigest(string filePath, string digest)
    {
        if (!digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) return false;
        var expected = digest[7..].Trim();
        using var stream = File.OpenRead(filePath);
        var actual = Convert.ToHexString(SHA256.HashData(stream));
        return actual.Equals(expected, StringComparison.OrdinalIgnoreCase);
    }
    public Process LaunchUpdater(UpdateInfo update, string zipPath)
    {
        if (!File.Exists(AppPaths.UpdaterExecutable))
            throw new FileNotFoundException("The internal updater component is missing.", AppPaths.UpdaterExecutable);

        var updaterDir = Path.Combine(Path.GetTempPath(), "RcloneTransferManager", "updater", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(updaterDir);
        var tempUpdater = Path.Combine(updaterDir, "RcloneTransferManager.Updater.exe");
        File.Copy(AppPaths.UpdaterExecutable, tempUpdater, true);
        var start = new ProcessStartInfo(tempUpdater) { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("--package"); start.ArgumentList.Add(zipPath);
        start.ArgumentList.Add("--target"); start.ArgumentList.Add(AppPaths.Root.TrimEnd(Path.DirectorySeparatorChar));
        start.ArgumentList.Add("--pid"); start.ArgumentList.Add(Environment.ProcessId.ToString());
        start.ArgumentList.Add("--restart"); start.ArgumentList.Add(Environment.ProcessPath ?? Path.Combine(AppPaths.Root, "RcloneTransferManager.exe"));
        start.ArgumentList.Add("--digest"); start.ArgumentList.Add(update.Digest);
        return Process.Start(start) ?? throw new InvalidOperationException("Could not start the updater process.");
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RcloneTransferManager", AppInfo.Version));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2026-03-10");
        return client;
    }
    private static bool ShouldCheckNow()
    {
        try
        {
            if (!File.Exists(AppPaths.UpdateStateFile)) return true;
            var text = File.ReadAllText(AppPaths.UpdateStateFile).Trim();
            return !DateTimeOffset.TryParse(text, out var last) || DateTimeOffset.UtcNow - last >= TimeSpan.FromHours(24);
        }
        catch { return true; }
    }

    private static void MarkChecked()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.PersistentData);
            File.WriteAllText(AppPaths.UpdateStateFile, DateTimeOffset.UtcNow.ToString("O"));
        }
        catch { }
    }
}
