using RcloneTransferManager.Models;
using RcloneTransferManager.Services;

var localSource = new ResolvedLocation(LocationKind.Local, @"C:\Input\ParentFolder", string.Empty, @"C:\Input\ParentFolder", null);
var localDestination = new ResolvedLocation(LocationKind.Local, @"D:\Target", string.Empty, @"D:\Target", null);
var sourceName = TransferService.GetSourceFolderName(localSource);
if (sourceName != "ParentFolder") throw new Exception($"Expected ParentFolder, got '{sourceName}'.");

var localPlanned = TransferService.AppendFolderToDestination(localDestination, sourceName);
if (localPlanned.Path != Path.Combine(@"D:\Target", "ParentFolder"))
    throw new Exception($"Local destination was not nested: {localPlanned.Path}");

var cloudDestination = new ResolvedLocation(LocationKind.GoogleDrive, "https://drive.google.com/drive/folders/dest", "google", string.Empty, "dest");
var cloudPlanned = TransferService.AppendFolderToDestination(cloudDestination, sourceName);
if (cloudPlanned.Path != "ParentFolder") throw new Exception($"Cloud destination was not nested: {cloudPlanned.Path}");

var googleName = TransferService.ParseGoogleFolderNameResponse("{\"name\":\"Google Source\",\"mimeType\":\"application/vnd.google-apps.folder\"}");
if (googleName != "Google Source") throw new Exception($"Google folder name parse failed: {googleName}");
if (!GoogleDriveFolderNameService.ShouldRetry(System.Net.HttpStatusCode.Forbidden)
    || !GoogleDriveFolderNameService.ShouldRetry((System.Net.HttpStatusCode)429)
    || !GoogleDriveFolderNameService.ShouldRetry(System.Net.HttpStatusCode.ServiceUnavailable)
    || GoogleDriveFolderNameService.ShouldRetry(System.Net.HttpStatusCode.NotFound))
    throw new Exception("Google folder lookup retry policy is incorrect.");

Console.WriteLine("Root folder path-planning checks passed.");

var repo = new DirectoryInfo(AppContext.BaseDirectory);
while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "rclone.exe"))) repo = repo.Parent;
if (repo is null) throw new Exception("Could not locate bundled rclone for integration check.");
var testInternal = Path.Combine(AppContext.BaseDirectory, "_internal");
Directory.CreateDirectory(testInternal);
var testRclone = Path.Combine(testInternal, "rclone.exe");
if (!File.Exists(testRclone)) File.Copy(Path.Combine(repo.FullName, "rclone.exe"), testRclone, false);

var tempRoot = Path.Combine(Path.GetTempPath(), "rtm-root-folder-test-" + Guid.NewGuid().ToString("N"));
var actualSource = Path.Combine(tempRoot, "MainFolder");
var actualDestination = Path.Combine(tempRoot, "Destination");
Directory.CreateDirectory(actualSource);
Directory.CreateDirectory(actualDestination);
await File.WriteAllTextAsync(Path.Combine(actualSource, "sample.txt"), "root-folder-regression");

var runner = new RcloneProcessRunner();
var log = new LogService();
var config = new RcloneConfigService(runner, log);
var service = new TransferService(runner, config, log);
var job = new TransferJob { Source = actualSource, Destination = actualDestination, Mode = TransferMode.Copy };
var result = await service.RunAsync(new TransferRequest(job, Array.Empty<string>()), null, null);
if (result.ExitCode != 0) throw new Exception("Local integration copy failed.");
if (!File.Exists(Path.Combine(actualDestination, "MainFolder", "sample.txt"))) throw new Exception("Main source folder was not preserved.");
if (File.Exists(Path.Combine(actualDestination, "sample.txt"))) throw new Exception("Source contents were flattened into destination.");
Directory.Delete(tempRoot, true);
Console.WriteLine("Root folder local integration check passed.");

var googleFolderId = Environment.GetEnvironmentVariable("RTM_TEST_GOOGLE_FOLDER_ID");
if (!string.IsNullOrWhiteSpace(googleFolderId))
{
    var folderNames = new GoogleDriveFolderNameService(runner);
    var liveFolderName = await folderNames.GetFolderNameAsync(googleFolderId);
    if (string.IsNullOrWhiteSpace(liveFolderName)) throw new Exception("Google Drive folder-name lookup failed.");
    Console.WriteLine("Google Drive folder-name lookup passed.");
}