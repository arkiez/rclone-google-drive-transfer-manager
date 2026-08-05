# Rclone Transfer Manager

Portable Windows x64 GUI for copying and synchronizing local folders,
Google Drive, and OneDrive through rclone.

Version: 1.1.0
Creator: Arkie'z K. Khositkhanawut

## Highlights

- Compact WPF interface; no Command Prompt required.
- Copy and Sync with conflict handling and Sync preview.
- Google Drive and OneDrive browser OAuth.
- Public direct-download file URL to a local folder without login.
- Cloud folder and shared links use the connected provider account.

## Build

Use the .NET 8 Windows Desktop SDK:

    dotnet build src\RcloneTransferManager\RcloneTransferManager.csproj --configuration Release
    .\scripts\package.ps1

The release package is created as
RcloneTransferManager-v1.1.0-win-x64.zip.

Runtime credentials are stored in the local data folder and are intentionally
ignored by Git. Never commit a populated data or logs folder.

See the user guide at docs\RcloneTransferManager-README.txt for usage and
security notes.
