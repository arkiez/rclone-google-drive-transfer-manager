# Rclone Transfer Manager

Portable Windows x64 GUI for copying from Google Drive or public file links
to Google Drive or local Windows folders through rclone.

Current release: **v2.2.3**. The application version is defined in `Version.props`.
Creator: Arkie'z K. Khositkhanawut

## Highlights

- Compact WPF interface; no Command Prompt required.
- Main window is fixed at 1200x520, fits its content without scrollbars, and can still be minimized.
- Source and Destination inputs use rounded fields with in-field Clear buttons.
- Source accepts Google Drive file/folder links and public direct file links only.
- Destination can be Google Drive or a local Windows folder.
- Copy mode with conflict handling; destination files are never deleted.
- Google Drive browser OAuth with persistent login in AppData.
- Transfers use the persistent Google config directly so refreshed OAuth tokens are not lost in temporary run configs.
- The Accounts window shows the connected Google email when available.
- Selected source folders are preserved as top-level folders at the destination.
- Built-in GitHub Releases updater with SHA-256 asset verification and automatic restart.
- Automatic update checks are throttled to once per 24 hours; About also provides a manual check.
- A completion popup confirms every successful transfer.
- The portable package has one clear program entry point; the updater is kept under `_internal`.

## Build

Use the .NET 8 Windows Desktop SDK:

    dotnet build src\RcloneTransferManager\RcloneTransferManager.csproj --configuration Release
    .\scripts\package.ps1

The release package is created as
`RcloneTransferManager-v<version>-win-x64.zip`.

After a verified commit is pushed to `main`, publish the current version to
GitHub Releases with:

    .\scripts\release.ps1

The public release repository is `arkiez/rclone-google-drive-transfer-manager`.
The application reads its latest published release without embedding a GitHub token.

After extraction, open RcloneTransferManager.exe. The bundled backend is kept
under _internal and is started automatically; do not open or move it. To
regenerate the checked-in program icon after changing its SVG source, run
.\scripts\generate-icon.ps1.

Google Drive credentials are stored under `%APPDATA%\RcloneTransferManager`
and are intentionally kept outside the release package. Never commit user
credentials or populated runtime logs.

## Versioning

Before committing or pushing a code change, update `Version.props` in the same
commit:

- Breaking or incompatible change: increment major and reset minor/patch.
- Backward-compatible feature: increment minor and reset patch.
- Backward-compatible bug fix or small behavior correction: increment patch.
- Documentation-only change: do not change the version.

Keep all visible versions and package names derived from `Version.props`, then
verify the result with:

    .\scripts\version-test.ps1

See the user guide at docs\RcloneTransferManager-README.txt for usage and
security notes.
