namespace RcloneTransferManager.Services;

public static class AppPaths
{
    public static string Root => AppContext.BaseDirectory;
    public static string Data => Path.Combine(Root, "data");
    public static string Logs => Path.Combine(Root, "logs");
    public static string JobsFile => Path.Combine(Data, "jobs.json");
    public static string ConfigFile => Path.Combine(Data, "rclone.conf");
    public static string RcloneExecutable => Path.Combine(Root, "rclone.exe");
    public static void Ensure() { Directory.CreateDirectory(Data); Directory.CreateDirectory(Logs); }
}
