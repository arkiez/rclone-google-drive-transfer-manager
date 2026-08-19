namespace RcloneTransferManager.Services;

public static class AppPaths
{
    public static string Root => AppContext.BaseDirectory;
    public static string Internal => Path.Combine(Root, "_internal");
    public static string Data => Path.Combine(Root, "data");
    public static string Logs => Path.Combine(Root, "logs");
    public static string PersistentData => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RcloneTransferManager");
    public static string ConfigFile => Path.Combine(PersistentData, "rclone.conf");
    public static string GoogleAccountIdentityFile => Path.Combine(PersistentData, "google-account.txt");
    public static string GoogleFolderNamesFile => Path.Combine(PersistentData, "google-folder-names.json");
    public static string UpdateStateFile => Path.Combine(PersistentData, "update-last-check.txt");
    public static string LegacyConfigFile => Path.Combine(Data, "rclone.conf");
    public static string RcloneExecutable => Path.Combine(Internal, "rclone.exe");
    public static string UpdaterExecutable => Path.Combine(Internal, "RcloneTransferManager.Updater.exe");
    public static string MissingRcloneMessage =>
        "The internal rclone component is missing.\n\n" +
        "Extract the complete ZIP again and keep the _internal folder beside RcloneTransferManager.exe. " +
        "If the file is still missing, check your antivirus quarantine.";

    public static void Ensure()
    {
        Directory.CreateDirectory(Data);
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(PersistentData);
        MigrateLegacyConfig();
        CleanupStaleRunConfigs();
    }

    private static void CleanupStaleRunConfigs()
    {
        try
        {
            foreach (var path in Directory.EnumerateFiles(Data, "run-*.conf", SearchOption.TopDirectoryOnly))
            {
                try { File.Delete(path); } catch { }
            }
        }
        catch { }
    }

    private static void MigrateLegacyConfig()
    {
        if (File.Exists(ConfigFile) || !File.Exists(LegacyConfigFile)) return;
        File.Copy(LegacyConfigFile, ConfigFile, false);
    }

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
