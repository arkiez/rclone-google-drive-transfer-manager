$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root "src\RcloneTransferManager"

$activeFiles = Get-ChildItem -LiteralPath $src -Recurse -File |
    Where-Object {
        $_.Extension -in @('.cs', '.xaml') -and
        $_.FullName -notmatch '\\(bin|obj)\\'
    }
$text = ($activeFiles | ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName }) -join "`n"

if ($text -match '(?i)onedrive|sharepoint|1drv') {
    throw "Active source still contains OneDrive/SharePoint support."
}
if ($text -match 'TransferMode\s*\{[^}]*Sync' -or $text -match '"sync"') {
    throw "Active source still contains Sync mode support."
}

$appPaths = Get-Content -Raw -LiteralPath (Join-Path $src "Services\AppPaths.cs")
if ($appPaths -notmatch 'SpecialFolder\.ApplicationData') {
    throw "Google Drive config is not stored in persistent AppData."
}
if ($appPaths -notmatch 'LegacyConfigFile') {
    throw "Legacy portable config migration is missing."
}

Write-Host "Breaking scope regression checks passed."
