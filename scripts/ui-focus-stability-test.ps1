$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$appXaml = Join-Path $root "src\RcloneTransferManager\App.xaml"
$text = Get-Content -Raw -LiteralPath $appXaml

$focusBlocks = [regex]::Matches(
    $text,
    '<Trigger Property="IsKeyboard(?:Focused|FocusWithin)" Value="True">.*?</Trigger>',
    [Text.RegularExpressions.RegexOptions]::Singleline)

foreach ($block in $focusBlocks) {
    if ($block.Value -match 'BorderThickness') {
        throw "Keyboard focus must not change BorderThickness because it causes layout shift."
    }
}

if ($focusBlocks.Count -lt 3) {
    throw "Expected keyboard focus styles were not found."
}

Write-Host "UI focus stability checks passed."
