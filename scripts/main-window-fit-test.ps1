$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$mainPath = Join-Path $root "src\RcloneTransferManager\MainWindow.xaml"
$monitorPath = Join-Path $root "src\RcloneTransferManager\Views\TransferMonitorWindow.xaml"
$main = Get-Content -Raw -LiteralPath $mainPath
$monitor = Get-Content -Raw -LiteralPath $monitorPath

if ($main.Contains('<ScrollViewer Grid.Row="1"')) { throw "MainWindow must not use a content ScrollViewer." }
if ($main.Contains('Width="760" HorizontalAlignment="Center"')) { throw "MainWindow content is still fixed at 760px." }
if (-not $main.Contains('<Grid Grid.Row="1" Margin="24,16,24,12">')) { throw "MainWindow fitted content grid is missing." }
if ($monitor -notmatch '<Window x:Class="RcloneTransferManager.Views.TransferMonitorWindow"') { throw "TransferMonitorWindow must remain unchanged/resizable." }
Write-Host "Main window fit regression checks passed."