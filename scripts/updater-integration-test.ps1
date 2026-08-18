$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
[xml]$props = Get-Content -Raw (Join-Path $root "Version.props")
$version = [string]$props.Project.PropertyGroup.Version
$zip = Join-Path $root "RcloneTransferManager-v$version-win-x64.zip"
$updater = Join-Path $root "dist\RcloneTransferManager\_internal\RcloneTransferManager.Updater.exe"
if (-not (Test-Path $zip) -or -not (Test-Path $updater)) { throw "Package the application before updater integration test." }
$target = Join-Path $env:TEMP ("rtm-updater-test-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force (Join-Path $target "data"),(Join-Path $target "logs") | Out-Null
Set-Content (Join-Path $target "data\keep.txt") "keep-data"
Set-Content (Join-Path $target "logs\keep.log") "keep-log"
$hash = (Get-FileHash -Algorithm SHA256 $zip).Hash.ToLowerInvariant()
$args = @("--package",$zip,"--target",$target,"--pid","999999","--restart",(Join-Path $env:WINDIR "System32\whoami.exe"),"--digest",("sha256:"+$hash))
$p = Start-Process $updater -ArgumentList $args -Wait -PassThru
if ($p.ExitCode -ne 0) { throw "Updater exited $($p.ExitCode)." }
foreach ($required in @("RcloneTransferManager.exe","_internal\rclone.exe","_internal\RcloneTransferManager.Updater.exe","data\keep.txt","logs\keep.log")) {
    if (-not (Test-Path (Join-Path $target $required))) { throw "Updater missing $required" }
}
if ((Get-Content -Raw (Join-Path $target "data\keep.txt")).Trim() -ne "keep-data") { throw "Updater changed data." }
if ((Get-Content -Raw (Join-Path $target "logs\keep.log")).Trim() -ne "keep-log") { throw "Updater changed logs." }
Remove-Item $target -Recurse -Force
Write-Host "Updater sandbox integration checks passed."
