using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace RcloneTransferManager.Services;

public sealed class GoogleDriveFolderNameService
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly object CacheLock = new();
    private readonly RcloneProcessRunner _runner;

    public GoogleDriveFolderNameService(RcloneProcessRunner runner) => _runner = runner;

    public async Task<string?> GetFolderNameAsync(string folderId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folderId)) return null;
        var cached = ReadCache(folderId);
        if (!string.IsNullOrWhiteSpace(cached)) return cached;
        var token = ReadAccessToken();
        if (token is null || token.Value.Expiry <= DateTimeOffset.UtcNow.AddMinutes(2))
        {
            await RefreshRcloneTokenAsync(cancellationToken);
            token = ReadAccessToken();
        }

        if (token is null) return cached;
        var response = await FetchFolderWithRetryAsync(folderId, token.Value.AccessToken, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            await RefreshRcloneTokenAsync(cancellationToken);
            token = ReadAccessToken();
            if (token is null) return cached;
            response = await FetchFolderWithRetryAsync(folderId, token.Value.AccessToken, cancellationToken);
        }
        using (response)
        {
            if (!response.IsSuccessStatusCode) return cached;
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var name = ParseFolderNameResponse(json);
            if (string.IsNullOrWhiteSpace(name)) return cached;
            SaveCache(folderId, name);
            return name;
        }
    }

    public static string? ParseFolderNameResponse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("mimeType", out var mimeType)
                || mimeType.GetString() != "application/vnd.google-apps.folder") return null;
            if (!root.TryGetProperty("name", out var name)) return null;
            return string.IsNullOrWhiteSpace(name.GetString()) ? null : name.GetString()!.Trim();
        }
        catch (JsonException) { return null; }
    }

    public static bool ShouldRetry(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.Forbidden or HttpStatusCode.RequestTimeout
        || (int)statusCode == 429
        || (int)statusCode >= 500;

    private static async Task<HttpResponseMessage> FetchFolderWithRetryAsync(string folderId, string accessToken, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var response = await FetchFolderAsync(folderId, accessToken, cancellationToken);
            if (!ShouldRetry(response.StatusCode) || attempt == 3) return response;
            response.Dispose();
            await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), cancellationToken);
        }
        throw new InvalidOperationException("Google Drive folder lookup retry loop did not return a response.");
    }

    private static async Task<HttpResponseMessage> FetchFolderAsync(string folderId, string accessToken, CancellationToken cancellationToken)
    {
        var url = $"https://www.googleapis.com/drive/v3/files/{Uri.EscapeDataString(folderId)}?fields=name%2CmimeType&supportsAllDrives=true";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private async Task RefreshRcloneTokenAsync(CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(new[]
        {
            "about", "google:", "--json", "--retries", "1", "--low-level-retries", "1",
            "--timeout", "20s", "--log-level", "ERROR"
        }, AppPaths.ConfigFile, null, cancellationToken);
        _ = result.ExitCode;
    }

    private static (string AccessToken, DateTimeOffset Expiry)? ReadAccessToken()
    {
        try
        {
            if (!File.Exists(AppPaths.ConfigFile)) return null;
            var inGoogle = false;
            foreach (var rawLine in File.ReadLines(AppPaths.ConfigFile))
            {
                var line = rawLine.Trim();
                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    inGoogle = line.Equals("[google]", StringComparison.OrdinalIgnoreCase);
                    continue;
                }
                if (!inGoogle) continue;
                var equals = line.IndexOf('=');
                if (equals < 0 || !line[..equals].Trim().Equals("token", StringComparison.OrdinalIgnoreCase)) continue;
                using var tokenJson = JsonDocument.Parse(line[(equals + 1)..].Trim());
                var root = tokenJson.RootElement;
                if (!root.TryGetProperty("access_token", out var accessToken)) return null;
                var token = accessToken.GetString();
                if (string.IsNullOrWhiteSpace(token)) return null;
                var expiry = DateTimeOffset.MaxValue;
                if (root.TryGetProperty("expiry", out var expiryElement)
                    && DateTimeOffset.TryParse(expiryElement.GetString(), out var parsedExpiry)) expiry = parsedExpiry;
                return (token, expiry);
            }
        }
        catch { }
        return null;
    }

    private static string? ReadCache(string folderId)
    {
        lock (CacheLock)
        {
            try
            {
                if (!File.Exists(AppPaths.GoogleFolderNamesFile)) return null;
                var values = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(AppPaths.GoogleFolderNamesFile));
                return values is not null && values.TryGetValue(folderId, out var name) ? name : null;
            }
            catch { return null; }
        }
    }


    private static void SaveCache(string folderId, string name)
    {
        lock (CacheLock)
        {
            try
            {
                Dictionary<string, string> values;
                if (File.Exists(AppPaths.GoogleFolderNamesFile))
                    values = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(AppPaths.GoogleFolderNamesFile)) ?? new();
                else values = new();
                values[folderId] = name;
                File.WriteAllText(AppPaths.GoogleFolderNamesFile, JsonSerializer.Serialize(values));
            }
            catch { }
        }
    }
}