using System.Security.Cryptography;
using RcloneTransferManager.Services;

const string json = """
{
  "tag_name":"v9.8.7",
  "body":"Release notes",
  "assets":[{
    "name":"RcloneTransferManager-v9.8.7-win-x64.zip",
    "browser_download_url":"https://example.invalid/update.zip",
    "digest":"sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
    "size":12345
  }]
}
""";

var update = UpdateService.ParseLatestRelease(json) ?? throw new Exception("Release JSON did not parse.");
if (update.Version != new Version(9, 8, 7)) throw new Exception("Version parse failed.");
if (update.Size != 12345 || update.DownloadUrl != "https://example.invalid/update.zip") throw new Exception("Asset parse failed.");
if (UpdateService.Repository != "arkiez/rclone-google-drive-transfer-manager") throw new Exception("Repository constant mismatch.");

var temp = Path.GetTempFileName();
await File.WriteAllTextAsync(temp, "update-regression");
var digest = "sha256:" + Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(temp))).ToLowerInvariant();
if (!UpdateService.VerifyDigest(temp, digest)) throw new Exception("Valid SHA-256 digest was rejected.");
if (UpdateService.VerifyDigest(temp, "sha256:" + new string('0', 64))) throw new Exception("Invalid SHA-256 digest was accepted.");
File.Delete(temp);
Console.WriteLine("Update release parsing and SHA-256 checks passed.");
