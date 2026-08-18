param(
    [string]$DotnetCommand = "dotnet"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$versionProps = Join-Path $root "Version.props"
if (-not (Test-Path -LiteralPath $versionProps -PathType Leaf)) {
    throw "Version.props not found: $versionProps"
}
[xml]$versionDocument = Get-Content -Raw -LiteralPath $versionProps
$version = [string]$versionDocument.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version.props must contain a major.minor.patch Version."
}

$project = Join-Path $root "src\RcloneTransferManager\RcloneTransferManager.csproj"
$updaterProject = Join-Path $root "src\RcloneTransferManager.Updater\RcloneTransferManager.Updater.csproj"
$publish = Join-Path $root "dist\RcloneTransferManager"
$updaterPublish = Join-Path $root "dist\RcloneTransferManager.Updater"
$zip = Join-Path $root "RcloneTransferManager-v$version-win-x64.zip"
$stagingZip = Join-Path $root "dist\RcloneTransferManager-v$version-win-x64.staging.zip"
$readme = Join-Path $root "docs\RcloneTransferManager-README.txt"

if (-not (Test-Path -LiteralPath $project)) { throw "Project not found: $project" }
if (-not (Test-Path -LiteralPath $updaterProject)) { throw "Updater project not found: $updaterProject" }
if (-not (Test-Path -LiteralPath (Join-Path $root "rclone.exe"))) { throw "Bundled rclone.exe not found." }
if (Test-Path -LiteralPath $publish) { Remove-Item -LiteralPath $publish -Recurse -Force }
if (Test-Path -LiteralPath $updaterPublish) { Remove-Item -LiteralPath $updaterPublish -Recurse -Force }
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
if (Test-Path -LiteralPath $stagingZip) { Remove-Item -LiteralPath $stagingZip -Force }

& $DotnetCommand publish $project --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }
& $DotnetCommand publish $updaterProject --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -p:TrimMode=partial -p:DebugType=None -p:DebugSymbols=false -o $updaterPublish
if ($LASTEXITCODE -ne 0) { throw "Updater publish failed with exit code $LASTEXITCODE." }
$updaterExe = Join-Path $updaterPublish "RcloneTransferManager.Updater.exe"
if (-not (Test-Path -LiteralPath $updaterExe -PathType Leaf)) { throw "Published updater executable is missing." }
Copy-Item -LiteralPath $updaterExe -Destination (Join-Path $publish "_internal\RcloneTransferManager.Updater.exe") -Force

$readmeTemplate = Get-Content -Raw -LiteralPath $readme
if (-not $readmeTemplate.Contains("{{VERSION}}")) {
    throw "Packaged README template is missing {{VERSION}}."
}
$readmeText = $readmeTemplate.Replace("{{VERSION}}", $version)
[IO.File]::WriteAllText(
    (Join-Path $publish "README.txt"),
    $readmeText,
    [Text.UTF8Encoding]::new($false))
New-Item -ItemType Directory -Path (Join-Path $publish "data") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $publish "logs") -Force | Out-Null

$rootExecutables = @(Get-ChildItem -LiteralPath $publish -File -Filter "*.exe")
if ($rootExecutables.Count -ne 1 -or $rootExecutables[0].Name -ne "RcloneTransferManager.exe") {
    throw "Package root must contain only RcloneTransferManager.exe."
}
$rootLibraries = @(Get-ChildItem -LiteralPath $publish -File -Filter "*.dll")
if ($rootLibraries.Count -ne 0) {
    throw "Package root contains loose DLL files: $($rootLibraries.Name -join ', ')"
}
$internalRclone = Join-Path $publish "_internal\rclone.exe"
$internalUpdater = Join-Path $publish "_internal\RcloneTransferManager.Updater.exe"
if (-not (Test-Path -LiteralPath $internalRclone -PathType Leaf)) { throw "Internal rclone backend not found: $internalRclone" }
if (-not (Test-Path -LiteralPath $internalUpdater -PathType Leaf)) { throw "Internal updater not found: $internalUpdater" }
foreach ($required in @("README.txt", "data", "logs")) {
    if (-not (Test-Path -LiteralPath (Join-Path $publish $required))) { throw "Required package item not found: $required" }
}

Compress-Archive -Path (Join-Path $publish "*") -DestinationPath $stagingZip -CompressionLevel Optimal

Add-Type -AssemblyName System.IO.Compression
$archive = [System.IO.Compression.ZipFile]::Open($stagingZip, [System.IO.Compression.ZipArchiveMode]::Update)
try {
    if (-not ($archive.Entries.FullName -contains "data/")) { [void]$archive.CreateEntry("data/") }
    if (-not ($archive.Entries.FullName -contains "logs/")) { [void]$archive.CreateEntry("logs/") }

    $entryNames = @($archive.Entries | ForEach-Object { $_.FullName.Replace([char]92, [char]47) })
    $rootExeEntries = @($entryNames | Where-Object { $_ -notmatch "/" -and [System.IO.Path]::GetExtension($_) -eq ".exe" })
    if ($rootExeEntries.Count -ne 1 -or $rootExeEntries[0] -ne "RcloneTransferManager.exe") {
        throw "ZIP validation failed: expected one root executable named RcloneTransferManager.exe."
    }
    if ($entryNames -notcontains "_internal/rclone.exe") { throw "ZIP validation failed: _internal/rclone.exe is missing." }
    if ($entryNames -notcontains "_internal/RcloneTransferManager.Updater.exe") { throw "ZIP validation failed: internal updater is missing." }
    if ($entryNames -notcontains "README.txt") { throw "ZIP validation failed: README.txt is missing." }
    if ($entryNames -notcontains "data/") { throw "ZIP validation failed: data/ is missing." }
    if ($entryNames -notcontains "logs/") { throw "ZIP validation failed: logs/ is missing." }
}
finally { $archive.Dispose() }

Move-Item -LiteralPath $stagingZip -Destination $zip
Write-Host "Created $zip"
