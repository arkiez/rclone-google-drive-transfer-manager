using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace RcloneTransferManager.Updater;

internal static class Program
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    private static int Main(string[] args)
    {
        try
        {
            var options = ParseArguments(args);
            var package = Require(options, "package");
            var target = Require(options, "target");
            var restart = Require(options, "restart");
            var digest = Require(options, "digest");
            var pid = int.Parse(Require(options, "pid"));

            WaitForProcess(pid);
            if (!VerifyDigest(package, digest)) throw new InvalidDataException("Update package SHA-256 verification failed.");
            Install(package, target);
            Process.Start(new ProcessStartInfo(restart) { UseShellExecute = true, WorkingDirectory = target });
            return 0;
        }
        catch (Exception ex)
        {
            MessageBoxW(IntPtr.Zero, ex.Message, "Rclone Transfer Manager Update Failed", 0x10);
            return 1;
        }
    }
    private static Dictionary<string, string> ParseArguments(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i + 1 < args.Length; i += 2)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal)) continue;
            values[args[i][2..]] = args[i + 1];
        }
        return values;
    }

    private static string Require(Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException($"Missing --{key} argument.");

    private static void WaitForProcess(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            if (!process.WaitForExit(30000)) throw new TimeoutException("The application did not close in time for the update.");
        }
        catch (ArgumentException) { }
    }

    private static bool VerifyDigest(string filePath, string digest)
    {
        if (!digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) return false;
        using var stream = File.OpenRead(filePath);
        var actual = Convert.ToHexString(SHA256.HashData(stream));
        return actual.Equals(digest[7..].Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static void Install(string package, string target)
    {
        var stage = Path.Combine(Path.GetTempPath(), "RcloneTransferManager", "install", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            ZipFile.ExtractToDirectory(package, stage, true);
            ValidateStage(stage);
            var files = Directory.EnumerateFiles(stage, "*", SearchOption.AllDirectories)
                .OrderBy(source => Path.GetRelativePath(stage, source).Equals("RcloneTransferManager.exe", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
            foreach (var source in files)
            {
                var relative = Path.GetRelativePath(stage, source);
                if (IsRuntimeData(relative)) continue;
                var destination = Path.Combine(target, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination, true);
            }
        }
        finally
        {
            try { Directory.Delete(stage, true); } catch { }
        }
    }

    private static void ValidateStage(string stage)
    {
        if (!File.Exists(Path.Combine(stage, "RcloneTransferManager.exe")))
            throw new InvalidDataException("The update package does not contain RcloneTransferManager.exe.");
        if (!File.Exists(Path.Combine(stage, "_internal", "rclone.exe")))
            throw new InvalidDataException("The update package does not contain the internal rclone component.");
    }

    private static bool IsRuntimeData(string relative)
    {
        var normalized = relative.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        return normalized.StartsWith("data" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("logs" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
