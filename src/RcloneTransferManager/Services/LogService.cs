using System.Text.RegularExpressions;

namespace RcloneTransferManager.Services;

public sealed class LogService
{
    private static readonly Regex SecretRegex = new("(?i)(token|password|client_secret)=([^\\s]+)", RegexOptions.Compiled);
    private static readonly Regex UrlRegex = new("(?i)https?://[^\\s]+", RegexOptions.Compiled);

    public string CreateJobLog(string jobName, string? rcloneVersion = null)
    {
        var safe = string.Join("_", jobName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        safe = string.IsNullOrWhiteSpace(safe) ? "transfer" : safe;
        var path = Path.Combine(AppPaths.Logs, $"{DateTime.Now:yyyyMMdd-HHmmss}_{safe}.log");
        WriteFile(path, $"{AppInfo.Name} v{AppInfo.Version} | {AppInfo.Creator}");
        WriteFile(path, $"Job: {jobName}");
        if (!string.IsNullOrWhiteSpace(rcloneVersion)) WriteFile(path, $"Engine: {rcloneVersion}");
        return path!;
    }

    public void Write(string category, string message) => WriteFile(Path.Combine(AppPaths.Logs, $"application-{DateTime.Now:yyyyMMdd}.log"), $"[{category}] {message}");
    public void WriteFile(string path, string message)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.AppendAllText(path, $"[{DateTime.Now:O}] {Sanitize(message)}{Environment.NewLine}");
    }
    private static string Sanitize(string message)
    {
        var sanitized = SecretRegex.Replace(message, "$1=[redacted]");
        return UrlRegex.Replace(sanitized, "[url redacted]");
    }
}

public static class AppInfo
{
    public const string Name = "Rclone Transfer Manager";
    public const string Version = "1.1.0";
    public const string Creator = "Arkie'z K. Khositkhanawut";
}
