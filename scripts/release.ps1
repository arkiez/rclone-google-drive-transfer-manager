param([string]$Notes = "")
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$repo = "arkiez/rclone-google-drive-transfer-manager"
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw "GitHub CLI (gh) is required." }
[xml]$props = Get-Content -Raw -LiteralPath (Join-Path $root "Version.props")
$version = [string]$props.Project.PropertyGroup.Version
$tag = "v$version"
$zip = Join-Path $root "RcloneTransferManager-v$version-win-x64.zip"
if (git status --porcelain) { throw "Commit or stash changes before creating a release." }
git fetch origin
if ((git rev-parse HEAD) -ne (git rev-parse origin/main)) { throw "Local main must match origin/main before release." }
if (-not (Test-Path -LiteralPath $zip)) { & (Join-Path $PSScriptRoot "package.ps1") }
if (-not (Test-Path -LiteralPath $zip)) { throw "Release package is missing: $zip" }
& (Join-Path $PSScriptRoot "version-test.ps1") -ExpectedVersion $version -ZipPath $zip
if ($LASTEXITCODE -ne 0) { throw "Version verification failed." }
$previousPreference = $ErrorActionPreference
$ErrorActionPreference = "SilentlyContinue"
gh release view $tag --repo $repo *> $null
$releaseExists = $LASTEXITCODE -eq 0
$ErrorActionPreference = $previousPreference
if ($releaseExists) {
    gh release upload $tag $zip --repo $repo --clobber
} elseif ([string]::IsNullOrWhiteSpace($Notes)) {
    gh release create $tag $zip --repo $repo --target main --title "Rclone Transfer Manager $tag" --generate-notes
} else {
    gh release create $tag $zip --repo $repo --target main --title "Rclone Transfer Manager $tag" --notes $Notes
}
if ($LASTEXITCODE -ne 0) { throw "GitHub Release creation/upload failed." }
Write-Host "Published $tag to https://github.com/$repo/releases/tag/$tag"
