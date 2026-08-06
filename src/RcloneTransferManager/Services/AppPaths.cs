namespace RcloneTransferManager.Services;

public static class AppPaths
{
    public static string Root => AppContext.BaseDirectory;
    public static string Internal => Path.Combine(Root, "_internal");
    public static string Data => Path.Combine(Root, "data");
    public static string Logs => Path.Combine(Root, "logs");
    public static string ConfigFile => Path.Combine(Data, "rclone.conf");
    public static string RcloneExecutable => Path.Combine(Internal, "rclone.exe");
    public static string MissingRcloneMessage =>
        "The internal rclone component is missing.\n\n" +
        "Extract the complete ZIP again and keep the _internal folder beside RcloneTransferManager.exe. " +
        "If the file is still missing, check your antivirus quarantine.";
    public static void Ensure() { Directory.CreateDirectory(Data); Directory.CreateDirectory(Logs); }

    public static void EnsureRcloneAvailable()
    {
        if (!File.Exists(RcloneExecutable))
            throw new FileNotFoundException(MissingRcloneMessage, RcloneExecutable);
    }

    public static bool TryDeleteLegacyJobsFile(out string error)
    {
        error = string.Empty;
        try
        {
            var path = Path.Combine(Data, "jobs.json");
            if (File.Exists(path)) File.Delete(path);
            return true;
        }
        catch (Exception ex)
        {
            error = $"Could not remove the retired saved-job data: {ex.Message}";
            return false;
        }
    }
}
