Rclone Transfer Manager v{{VERSION}}
Created by Arkie'z K. Khositkhanawut

Rclone Transfer Manager is a Windows x64 desktop utility for copying files
through rclone without opening Command Prompt.

Supported locations
-------------------
- Google Drive file/folder links as Source
- Public direct-download file links as Source (no login required)
- Google Drive folder links as Destination
- Local Windows folders as Destination (paste a path or use Browse)

Quick start
-----------
1. Extract the ZIP and open RcloneTransferManager.exe.
2. Open Accounts and connect Google Drive when needed. The Accounts window shows the connected Google email when available.
3. Paste a Google Drive file/folder link or public direct file link into Source.
4. Choose Cloud or Local for Destination, then paste a Google Drive folder link or choose a local folder with Browse.
5. Select Start. The application uses Copy mode only.
6. A confirmation popup appears when the transfer finishes successfully.

Copy safety
-----------
Copy preserves the selected source folder as a folder inside the destination,
then adds new files and updates changed files. It never deletes destination
files. If a changed destination file is found, choose Overwrite or Skip.

Google Drive login
------------------
Google Drive authorization is stored at:
  %APPDATA%\RcloneTransferManager\rclone.conf

This keeps the login available when the application folder is replaced or
updated. Existing data\rclone.conf is migrated automatically when needed.
The release ZIP never contains user credentials.
Transfers use the persistent Google config directly, so refreshed OAuth tokens remain saved across transfers and application updates.

Updates
-------
The application checks GitHub Releases at startup at most once every 24 hours.
You can also open About and select Check for updates at any time.
When a newer release is available, choose Update Now to download the release ZIP.
The SHA-256 digest published by GitHub is verified before the updater runs.
The updater replaces application files and restarts the program while preserving
Google login data, the local data folder, and transfer logs.

Notes
-----
- Google Drive folders require the connected Google account to have access.
- Public direct-download files can be copied to Local without login.
- A read-only shared folder can be a source but cannot be a destination.
- This release supports one Google Drive account per Windows user profile.
- Pause stops the current rclone process. Resume starts the same Copy again;
  already completed identical files are skipped by rclone.
- Transfer logs are stored in the logs folder beside the executable.

Release
-------
Product: Rclone Transfer Manager
Version: {{VERSION}}
Creator: Arkie'z K. Khositkhanawut
