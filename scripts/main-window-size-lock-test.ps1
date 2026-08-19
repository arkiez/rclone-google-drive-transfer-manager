$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$mainPath = Join-Path $root "src\RcloneTransferManager\MainWindow.xaml"
$monitorPath = Join-Path $root "src\RcloneTransferManager\Views\TransferMonitorWindow.xaml"
$main = Get-Content -Raw -LiteralPath $mainPath
$monitor = Get-Content -Raw -LiteralPath $monitorPath

foreach ($required in @('Width="1200"','Height="520"','MinWidth="1200"','MaxWidth="1200"','MinHeight="520"','MaxHeight="520"','ResizeMode="CanMinimize"')) {
    if (-not $main.Contains($required)) { throw "MainWindow is not fully size-locked: missing $required" }
}
if ($monitor.Contains('ResizeMode="CanMinimize"') -or $monitor.Contains('ResizeMode="NoResize"')) {
    throw "TransferMonitorWindow must remain resizable."
}
Write-Host "Main window size-lock regression checks passed."
