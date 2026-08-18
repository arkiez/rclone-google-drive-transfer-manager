$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root "src\RcloneTransferManager"
$xaml = Get-Content -Raw -LiteralPath (Join-Path $src "MainWindow.xaml")
$code = Get-Content -Raw -LiteralPath (Join-Path $src "MainWindow.xaml.cs")

foreach ($forbidden in @('SourceCloudRadio','SourceLocalRadio','BrowseSourceButton','BrowseSource_Click','SourceLocationMode_Checked')) {
    if ($xaml -match [regex]::Escape($forbidden) -or $code -match [regex]::Escape($forbidden)) {
        throw "Source UI still contains local/cloud selector or Browse: $forbidden"
    }
}
if ($xaml -notmatch 'DestinationCloudRadio' -or $xaml -notmatch 'DestinationLocalRadio' -or $xaml -notmatch 'BrowseDestinationButton') {
    throw "Destination Cloud/Local selector or Browse was removed unexpectedly."
}
if ($code -notmatch 'TryValidateSelectedLocation\(source,\s*cloudMode:\s*true,\s*isSource:\s*true') {
    throw "Source validation is not locked to cloud/public-link mode."
}
if ($code -notmatch 'UpdateLocationStatus\(SourceBox,\s*SourceStatus,\s*cloudMode:\s*true,\s*isSource:\s*true') {
    throw "Source status is not locked to cloud/public-link mode."
}
Write-Host "Source cloud-only regression checks passed."