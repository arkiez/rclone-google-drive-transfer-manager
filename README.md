# Rclone Transfer Manager

Portable Windows x64 GUI for copying and synchronizing local folders,
Google Drive, and OneDrive through rclone.

The current application version is defined in `Version.props`.
Creator: Arkie'z K. Khositkhanawut

## Highlights

- Compact WPF interface; no Command Prompt required.
- Explicit Cloud or Local selectors for each transfer location.
- Copy and Sync with conflict handling and Sync preview.
- Google Drive and OneDrive browser OAuth.
- Public direct-download file URL to a local folder without login.
- Cloud folder and shared links use the connected provider account.
- A completion popup confirms every successful transfer.
- The portable package has one clear program entry point.

## Build

Use the .NET 8 Windows Desktop SDK:

    dotnet build src\RcloneTransferManager\RcloneTransferManager.csproj --configuration Release
    .\scripts\package.ps1

The release package is created as
`RcloneTransferManager-v<version>-win-x64.zip`.

After extraction, open RcloneTransferManager.exe. The bundled backend is kept
under _internal and is started automatically; do not open or move it. To
regenerate the checked-in program icon after changing its SVG source, run
.\scripts\generate-icon.ps1.

Runtime credentials are stored in the local data folder and are intentionally
ignored by Git. Never commit a populated data or logs folder.

See the user guide at docs\RcloneTransferManager-README.txt for usage and
security notes.
