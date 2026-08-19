param(
    [string]$ExpectedVersion = "2.2.2",
    [string]$BuildExe = "",
    [string]$PackageRoot = "",
    [string]$ZipPath = "",
    [string]$DotnetCommand = "dotnet"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$propsPath = Join-Path $root "Version.props"
$projectPath = Join-Path $root "src\RcloneTransferManager\RcloneTransferManager.csproj"

if (-not (Test-Path -LiteralPath $propsPath -PathType Leaf)) {
    throw "Version.props is missing."
}

[xml]$props = Get-Content -Raw -LiteralPath $propsPath
$version = [string]$props.Project.PropertyGroup.Version
$fileVersion = [string]$props.Project.PropertyGroup.FileVersion
$assemblyVersion = [string]$props.Project.PropertyGroup.AssemblyVersion

if ($version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must use major.minor.patch: $version"
}
if ($version -ne $ExpectedVersion) {
    throw "Expected $ExpectedVersion but found $version."
}
if ($fileVersion -ne "$ExpectedVersion.0") {
    throw "Unexpected FileVersion: $fileVersion"
}
if ($assemblyVersion -ne "$ExpectedVersion.0") {
    throw "Unexpected AssemblyVersion: $assemblyVersion"
}

$evaluatedJson = & $DotnetCommand msbuild $projectPath `
    -getProperty:Version `
    -getProperty:FileVersion `
    -getProperty:AssemblyVersion
if ($LASTEXITCODE -ne 0) {
    throw "MSBuild could not evaluate application version properties."
}
$evaluated = $evaluatedJson | ConvertFrom-Json
if ($evaluated.Properties.Version -ne $ExpectedVersion) {
    throw "Evaluated project Version mismatch: $($evaluated.Properties.Version)"
}
if ($evaluated.Properties.FileVersion -ne "$ExpectedVersion.0") {
    throw "Evaluated project FileVersion mismatch: $($evaluated.Properties.FileVersion)"
}
if ($evaluated.Properties.AssemblyVersion -ne "$ExpectedVersion.0") {
    throw "Evaluated project AssemblyVersion mismatch: $($evaluated.Properties.AssemblyVersion)"
}

if (-not [string]::IsNullOrWhiteSpace($BuildExe)) {
    $resolvedExe = (Resolve-Path -LiteralPath $BuildExe).Path
    $metadata = [Diagnostics.FileVersionInfo]::GetVersionInfo($resolvedExe)
    if ($metadata.FileVersion -ne "$ExpectedVersion.0") {
        throw "Built EXE FileVersion mismatch: $($metadata.FileVersion)"
    }
    if ($metadata.ProductVersion -notlike "$ExpectedVersion*") {
        throw "Built EXE ProductVersion mismatch: $($metadata.ProductVersion)"
    }
}

if (-not [string]::IsNullOrWhiteSpace($PackageRoot)) {
    $packagedReadmePath = Join-Path $PackageRoot "README.txt"
    if (-not (Test-Path -LiteralPath $packagedReadmePath -PathType Leaf)) {
        throw "Packaged README is missing."
    }
    $packagedReadme = Get-Content -Raw -LiteralPath $packagedReadmePath
    if ($packagedReadme -notmatch [regex]::Escape("Version: $ExpectedVersion")) {
        throw "Packaged README version mismatch."
    }
    if ($packagedReadme.Contains("{{VERSION}}")) {
        throw "Packaged README still contains the version token."
    }

    $rootExecutables = @(Get-ChildItem -LiteralPath $PackageRoot -File -Filter "*.exe")
    if ($rootExecutables.Count -ne 1 -or $rootExecutables[0].Name -ne "RcloneTransferManager.exe") {
        throw "Portable root executable layout changed."
    }
    if (-not (Test-Path -LiteralPath (Join-Path $PackageRoot "_internal\rclone.exe") -PathType Leaf)) {
        throw "Internal rclone backend is missing."
    }
    if (-not (Test-Path -LiteralPath (Join-Path $PackageRoot "_internal\RcloneTransferManager.Updater.exe") -PathType Leaf)) {
        throw "Internal updater is missing."
    }
}

if (-not [string]::IsNullOrWhiteSpace($ZipPath)) {
    $expectedName = "RcloneTransferManager-v$ExpectedVersion-win-x64.zip"
    if ((Split-Path -Leaf $ZipPath) -ne $expectedName) {
        throw "ZIP name mismatch: $ZipPath"
    }
    if (-not (Test-Path -LiteralPath $ZipPath -PathType Leaf)) {
        throw "ZIP is missing: $ZipPath"
    }
}

Write-Host "Version verification passed for $ExpectedVersion."
