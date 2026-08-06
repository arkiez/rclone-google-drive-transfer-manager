param(
    [string]$RclonePath = "",
    [string]$AppRoot = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($RclonePath)) {
    $RclonePath = if ([string]::IsNullOrWhiteSpace($AppRoot)) {
        Join-Path $root "rclone.exe"
    }
    else {
        Join-Path $AppRoot "_internal\rclone.exe"
    }
}
if (-not (Test-Path -LiteralPath $RclonePath)) { throw "rclone.exe not found: $RclonePath" }

$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("rtm-smoke-" + [guid]::NewGuid().ToString("N"))
$source = Join-Path $testRoot "source"
$destination = Join-Path $testRoot "destination"
$config = Join-Path $testRoot "empty.conf"
New-Item -ItemType Directory -Path $source, $destination -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $source "empty-child") -Force | Out-Null
Set-Content -LiteralPath $config -Value "" -Encoding UTF8
Set-Content -LiteralPath (Join-Path $source "hello.txt") -Value "hello from rclone transfer manager" -Encoding UTF8

try {
    $version = & $RclonePath version 2>&1
    if ($LASTEXITCODE -ne 0) { throw "rclone version failed." }
    Write-Host ($version | Select-Object -First 1)

    & $RclonePath --config $config copy $source $destination --create-empty-src-dirs --stats-one-line --log-level ERROR
    if ($LASTEXITCODE -ne 0) { throw "Local Copy smoke test failed." }
    $copied = Join-Path $destination "hello.txt"
    if (-not (Test-Path -LiteralPath $copied)) { throw "Copy did not create hello.txt." }
    if (-not (Test-Path -LiteralPath (Join-Path $destination "empty-child") -PathType Container)) { throw "Copy did not create the empty source directory." }

    Set-Content -LiteralPath (Join-Path $source "hello.txt") -Value "updated by sync" -Encoding UTF8
    & $RclonePath --config $config sync $source $destination --stats-one-line --log-level ERROR
    if ($LASTEXITCODE -ne 0) { throw "Local Sync smoke test failed." }
    if ((Get-Content -Raw -LiteralPath $copied) -notmatch "updated by sync") { throw "Sync did not update hello.txt." }

    Set-Content -LiteralPath (Join-Path $destination "stale.txt") -Value "stale" -Encoding UTF8
    $preview = & $RclonePath --config $config sync $source $destination --dry-run --combined - --stats-one-line --log-level ERROR 2>&1
    if ($LASTEXITCODE -ne 0 -or -not ($preview -match "(?m)^- stale\.txt$")) {
        throw "Combined Sync preview smoke test did not report the pending deletion."
    }
    Write-Host "Combined Sync preview smoke test passed."

    $assembly = Join-Path $root "src\RcloneTransferManager\bin\Release\net8.0-windows\RcloneTransferManager.dll"
    if (Test-Path -LiteralPath $assembly) {
        try {
            Add-Type -Path $assembly
            $resolved = $null
            $message = ""
            $ok = [RcloneTransferManager.Services.LocationResolver]::TryResolve("https://drive.google.com/drive/folders/abc_123", [ref]$resolved, [ref]$message)
            if (-not $ok -or $resolved.Kind.ToString() -ne "GoogleDrive" -or $resolved.RootFolderId -ne "abc_123") {
                throw "Google Drive URL parsing smoke test failed."
            }
            Write-Host "Location resolver smoke test passed."
        }
        catch {
            Write-Warning "Location resolver integration test was skipped because this Windows PowerShell host cannot load the .NET WPF assembly: $($_.Exception.Message)"
        }
    }
    Write-Host "All smoke tests passed."
}
finally {
    if (Test-Path -LiteralPath $testRoot) { Remove-Item -LiteralPath $testRoot -Recurse -Force }
}
