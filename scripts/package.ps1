param(
    [string]$DotnetCommand = "dotnet"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\RcloneTransferManager\RcloneTransferManager.csproj"
$publish = Join-Path $root "dist\RcloneTransferManager"
$zip = Join-Path $root "RcloneTransferManager-v1.1.0-win-x64.zip"
$readme = Join-Path $root "docs\RcloneTransferManager-README.txt"

if (-not (Test-Path -LiteralPath $project)) { throw "Project not found: $project" }
if (-not (Test-Path -LiteralPath (Join-Path $root "rclone.exe"))) { throw "Bundled rclone.exe not found." }
if (Test-Path -LiteralPath $publish) { Remove-Item -LiteralPath $publish -Recurse -Force }
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }

& $DotnetCommand publish $project --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=false -p:DebugType=None -p:DebugSymbols=false -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }

Copy-Item -LiteralPath $readme -Destination (Join-Path $publish "README.txt")
New-Item -ItemType Directory -Path (Join-Path $publish "data") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $publish "logs") -Force | Out-Null
Compress-Archive -Path (Join-Path $publish "*") -DestinationPath $zip -CompressionLevel Optimal

Add-Type -AssemblyName System.IO.Compression
$archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Update)
try {
    if (-not ($archive.Entries.FullName -contains "data/")) { [void]$archive.CreateEntry("data/") }
    if (-not ($archive.Entries.FullName -contains "logs/")) { [void]$archive.CreateEntry("logs/") }
}
finally { $archive.Dispose() }

Write-Host "Created $zip"
