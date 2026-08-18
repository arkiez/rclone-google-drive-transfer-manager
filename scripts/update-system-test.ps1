$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root "src\RcloneTransferManager"
$updateService = Join-Path $src "Services\UpdateService.cs"
$mainCode = Join-Path $src "MainWindow.xaml.cs"
$aboutXaml = Join-Path $src "Views\AboutWindow.xaml"
$updaterProject = Join-Path $root "src\RcloneTransferManager.Updater\RcloneTransferManager.Updater.csproj"
$package = Join-Path $root "scripts\package.ps1"
$release = Join-Path $root "scripts\release.ps1"
foreach ($required in @($updateService,$aboutXaml,$updaterProject,$release)) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Missing update component: $required" }
}
$serviceText = Get-Content -Raw -LiteralPath $updateService
if ($serviceText -notmatch 'arkiez/rclone-google-drive-transfer-manager') { throw "Updater repository is incorrect." }
if ($serviceText -notmatch 'sha256:') { throw "SHA-256 release digest verification is missing." }
$mainText = Get-Content -Raw -LiteralPath $mainCode
if ($mainText -notmatch 'CheckForUpdatesOnStartupAsync') { throw "Startup update check is missing." }
$aboutText = Get-Content -Raw -LiteralPath $aboutXaml
if ($aboutText -notmatch 'Check for updates') { throw "About window update action is missing." }
$packageText = Get-Content -Raw -LiteralPath $package
if ($packageText -notmatch 'RcloneTransferManager\.Updater\.exe') { throw "Updater is not included in package." }
Write-Host "Update system regression checks passed."