$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$svc = Get-Content -Raw (Join-Path $root "src\RcloneTransferManager\Services\RcloneConfigService.cs")
$app = Get-Content -Raw (Join-Path $root "src\RcloneTransferManager\Services\AppPaths.cs")

if ($svc -match 'run-.*\.conf' -or $svc -match 'File\.Copy\(AppPaths\.ConfigFile') {
    throw "Transfer flow still copies the persistent Google config into a temporary run config."
}
if ($svc -notmatch 'root_folder_id=') {
    throw "Google root folder IDs are not expressed as an rclone connection-string override."
}
if ($svc -notmatch 'new PreparedConfig\(AppPaths\.ConfigFile') {
    throw "Prepared transfers do not use the persistent AppData config directly."
}
if ($app -notmatch 'CleanupStaleRunConfigs') {
    throw "Startup cleanup for legacy run-*.conf files is missing."
}
Write-Host "OAuth persistence regression checks passed."