$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$app = Get-Content -Raw (Join-Path $root "src\RcloneTransferManager\App.xaml")
$xaml = Get-Content -Raw (Join-Path $root "src\RcloneTransferManager\MainWindow.xaml")
$code = Get-Content -Raw (Join-Path $root "src\RcloneTransferManager\MainWindow.xaml.cs")

if ($app -notmatch 'x:Key="RoundedInputTextBox"' -or $app -notmatch 'CornerRadius="8"') {
    throw "Rounded input TextBox style is missing."
}
foreach ($name in @('SourceClearButton','DestinationClearButton')) {
    if ($xaml -notmatch [regex]::Escape('x:Name="' + $name + '"')) { throw "Missing input clear button: $name" }
}
if ($xaml -notmatch 'Style="\{StaticResource RoundedInputTextBox\}"') { throw "Input boxes are not using the rounded style." }
if ($xaml -notmatch 'BrowseDestinationButton') { throw "Destination Browse button was removed." }
if ($code -notmatch 'ClearSource_Click' -or $code -notmatch 'ClearDestination_Click' -or $code -notmatch 'UpdateClearButton') {
    throw "Clear button behavior is incomplete."
}
Write-Host "Rounded input and Clear button regression checks passed."