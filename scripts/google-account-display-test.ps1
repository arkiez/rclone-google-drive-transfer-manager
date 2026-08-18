$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$service = Get-Content -Raw (Join-Path $root "src\RcloneTransferManager\Services\RcloneConfigService.cs")
$view = Get-Content -Raw (Join-Path $root "src\RcloneTransferManager\Views\AccountsWindow.xaml")
$code = Get-Content -Raw (Join-Path $root "src\RcloneTransferManager\Views\AccountsWindow.xaml.cs")

if (-not $service.Contains("GetGoogleAccountIdentityAsync")) { throw "Google account identity service is missing." }
if (-not $service.Contains("--drive-auth-owner-only")) { throw "Account lookup must use the authenticated owner filter." }
if (-not $view.Contains("GoogleAccountIdentity")) { throw "Google account identity label is missing." }
if (-not $code.Contains("Connected as:")) { throw "Connected account email is not displayed." }

Write-Host "Google account display checks passed."
