using System.Reflection;
using System.Text.RegularExpressions;

namespace RcloneTransferManager.Services;

public sealed class LogService
{
    private static readonly Regex SecretRegex = new("(?i)(token|password|client_secret)=([^\\s]+)", RegexOptions.Compiled);
    private static readonly Regex UrlRegex = new("(?i)https?://[^\\s]+", RegexOptions.Compiled);

    public string CreateTransferLog(string transferName, string? rcloneVersion = null)
    {
        var safe = string.Join("_", transferName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        safe = string.IsNullOrWhiteSpace(safe) ? "transfer" : safe;
        var path = Path.Combine(AppPaths.Logs, $"{DateTime.Now:yyyyMMdd-HHmmss}_{safe}.log");
        WriteFile(path, $"{AppInfo.Name} v{AppInfo.Version} | {AppInfo.Creator}");
        WriteFile(path, $"Transfer: {transferName}");
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
    public static string Version { get; } =
        typeof(AppInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+')[0]
        ?? typeof(AppInfo).Assembly.GetName().Version?.ToString(3)
        ?? "unknown";
    public const string Creator = "Arkie'z K. Khositkhanawut";
}
