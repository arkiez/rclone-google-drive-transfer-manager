Rclone Transfer Manager v1.1.1
Created by Arkie'z K. Khositkhanawut

Rclone Transfer Manager is a portable Windows x64 desktop utility for copying
and synchronizing files through rclone without opening Command Prompt.

Supported locations
-------------------
- Local Windows folders (paste a path or use Browse)
- Public direct-download file links to a local folder (no login required)
- Google Drive folder links
- OneDrive, OneDrive short links, and SharePoint folder links

Quick start
-----------
1. Extract the ZIP to a folder you can write to.
2. Open RcloneTransferManager.exe.
   This is the only program file you need to open.
3. Open Accounts and connect Google Drive or OneDrive when needed. The login
   flow opens in your default browser.
4. Select Cloud or Local independently for Source and Destination.
5. Paste a cloud link, paste a local folder path, or use Browse in Local mode.
6. Choose Copy (the safe default) or Sync, then select Start.
7. A confirmation popup appears when the transfer finishes successfully.

Copy and Sync safety
--------------------
Copy adds new files and updates changed files. It does not delete destination
files. If a changed destination file is found, the app asks whether to
overwrite or skip it.

Sync makes the destination match the source. The app always shows a preview
before Sync starts. Review the table carefully, especially Delete entries,
then choose Apply Sync only when it is correct.

Portable data and credentials
-----------------------------
The app keeps its local data beside the executable:

  _internal\rclone.exe Required transfer backend; do not open or move it
  data\rclone.conf     OAuth configuration and tokens
  logs\                Saved transfer logs and diagnostics

The release ZIP contains an empty data and logs folder. Authentication must be
performed separately on each machine. Do not share a populated data folder or
rclone.conf; it contains credentials for the connected accounts.

Keep the complete extracted folder together. If the app reports that the
internal rclone component is missing, extract the full ZIP again and check
whether antivirus software quarantined _internal\rclone.exe.

Notes
-----
- Cloud links work only when the connected account has access to the folder.
- A public direct-download file link can be copied to a local folder without
  login. It supports Copy only; a public folder link still requires provider
  login.
- A read-only shared link can be used as a source but cannot be used as a
  destination.
- This release supports one Google Drive account and one OneDrive account per
  machine.
- Pause stops the current rclone process. Resume safely starts the same job
  again; already completed identical files are skipped by rclone.
- The complete activity log is saved in the logs folder.

Release
-------
Product: Rclone Transfer Manager
Version: 1.1.1
Creator: Arkie'z K. Khositkhanawut
