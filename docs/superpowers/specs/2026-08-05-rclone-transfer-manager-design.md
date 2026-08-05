# Rclone Transfer Manager — Design Specification

Date: 2026-08-05  
Status: Draft for written review; design approved in conversation  
Owner/Creator: Arkie'z K. Khositkhanawut  
Initial version: 1.0.0

## 1. Summary

Rclone Transfer Manager is a portable Windows desktop application that gives non-technical users a clear GUI for moving files through rclone without opening CMD. It supports local folders, Google Drive, and OneDrive as either source or destination.

The application is a WPF/.NET self-contained `win-x64` desktop app. It bundles the pinned `rclone.exe` binary, runs it invisibly, and translates its progress and errors into an English UI.

## 2. Goals

- Work on Windows 10 and Windows 11 without an installer or separate .NET installation.
- Let users paste Google Drive/OneDrive folder links or enter local paths.
- Support local ↔ cloud, cloud ↔ local, and cloud ↔ cloud transfers.
- Offer `Copy` and `Sync`, with `Copy` selected by default.
- Save transfer jobs for reuse.
- Require a review and explicit confirmation before Sync can delete destination files.
- Show progress, current file, remaining time, Log, Pause, Cancel, and Resume.
- Keep Google Drive and OneDrive authentication separate per machine.
- Produce a distributable ZIP that contains no user credentials.

## 3. Non-goals for v1.0.0

- No browser-based application UI.
- No CMD workflow exposed to users.
- No scheduled or automatic transfers.
- No two-way bisync.
- No multiple accounts per provider; the data model must allow adding this later.
- No cloud folder picker; cloud locations are entered as links.
- No installer, Windows service, or background daemon.

## 4. Product identity and release

- Product name: `Rclone Transfer Manager`
- Application version: `1.0.0`
- Creator: `Arkie'z K. Khositkhanawut`
- Target package: `RcloneTransferManager-v1.0.0-win-x64.zip`
- The version appears in the window title, About dialog, executable metadata, ZIP name, and every Log header.
- Versioning uses semantic intent: patch releases fix defects, minor releases add compatible features, and major releases contain breaking changes.
- The ZIP contains the application, the pinned rclone binary, a README, and empty `data`/`logs` directories. It must not contain a token or an existing `rclone.conf` with credentials.

## 5. User experience

### 5.1 Visual language

The product uses a light professional Windows utility style based on flat design and minimalism:

- Primary: `#2563EB`
- Background: `#F8FAFC`
- Foreground: `#0F172A`
- Muted surface: `#F1F5FD`
- Border: `#E4ECFC`
- Warning: amber/orange, used with text and an icon
- Destructive: `#DC2626`, used for delete warnings and destructive confirmation
- UI typography: Segoe UI for Windows portability
- Paths and Log content: Cascadia Mono or Consolas
- Motion is subtle, around 150–300 ms, and never required to understand state
- Visible keyboard focus and reduced-motion-friendly transitions are required
- Icons must be labeled; color alone must not communicate success, warning, or failure

### 5.2 Main window

The primary screen is a focused transfer form:

1. Product header with version and access to About, Saved jobs, and Accounts.
2. A Saved job dropdown for loading a previously saved setup.
3. A stacked `Source` field.
4. A stacked `Destination` field.
5. Each location field supports paste, validation, provider detection, and `Browse` for local folders.
6. A segmented mode control with `Copy` selected by default and `Sync` beside it.
7. `Save Job` and `Start Transfer` actions.

Source and destination fields show a provider status after parsing:

- Local folder
- Google Drive
- OneDrive
- Invalid or unsupported location
- Login required

### 5.3 Accounts panel

Accounts are managed in a dedicated Accounts panel. It shows one Google Drive connection and one OneDrive connection per machine, each with:

- Connected/not connected state
- Account identity when available
- Connect, Reconnect, and Disconnect actions
- A statement that tokens stay on this machine

Connecting opens the provider's browser-based OAuth flow. The app does not ask users to paste passwords and never writes passwords to its files or Logs.

### 5.4 Transfer monitor

Starting a job opens a dedicated monitor window or view containing:

- Job name and operation mode
- Source and destination summary
- Progress bar and percentage
- Bytes transferred and total bytes when available
- Estimated remaining time when available
- Current file and recent file activity
- `Pause`, `Cancel`, and `Resume` actions
- `View Log` action

Pause stops at a safe process boundary. Resume reruns the same job and lets rclone skip already-completed identical files.

### 5.5 Sync preview

Sync always enters a review state before applying changes. The review table lists each planned change with a text label and status:

- Add
- Update
- Delete

The review shows totals and highlights Delete entries with warning styling. `Cancel` leaves all files unchanged. `Confirm Sync` is the only action that proceeds to the destructive operation. If the source or destination changes between preview and execution, the app refreshes the preview or reports that the review is stale.

### 5.6 Saved jobs

Saved jobs are loaded through a compact dropdown on the main form. Each job includes:

- User-defined name
- Source location
- Destination location
- Operation mode
- Last-run status and timestamp

Loading a job fills the form without starting it. The user must still press `Start Transfer`.

### 5.7 Copy conflicts

Copy skips identical files. When a changed file conflicts with an existing destination file, the UI asks per file whether to overwrite or skip. The decision is shown in the monitor and Log.

## 6. Architecture

The application is split into focused units:

### 6.1 WPF presentation layer

MVVM views and view models own screen state, commands, validation messages, focus behavior, and accessibility names. UI code does not construct rclone commands directly.

### 6.2 Core job layer

The job layer owns the `TransferJob` model, saved-job persistence, validation, operation selection, and the state machine:

`Draft → Validating → Previewing → Ready → Running → Paused/Cancelled/Completed/Failed`

### 6.3 Location resolver

The resolver parses local paths and supported Google Drive/OneDrive folder URLs, detects the provider, normalizes the location, and reports unsupported or ambiguous links. Shared folders are allowed when the connected account has the required permission. A read-only shared link can be a source but cannot be a destination.

### 6.4 Authentication/configuration layer

The app invokes rclone's provider authentication flow and uses a portable config path under `data`. The release ZIP contains no credentials. A newly copied program folder requires authentication on the new machine.

The app should apply user-only Windows file permissions to the credential-bearing config where possible and warn if the file cannot be protected. Credentials and OAuth tokens are never written to Logs.

### 6.5 Rclone adapter

The adapter starts the bundled `rclone.exe` with `UseShellExecute=false`, `CreateNoWindow=true`, an explicit config path, and structured progress/output flags. It passes arguments as process arguments rather than constructing a shell command, preventing quoting and command-injection problems.

The adapter translates process output and exit codes into typed application events:

- Transfer progress
- Current file
- Warning
- Authentication failure
- Permission failure
- Network failure
- Disk/storage failure
- Completed
- Cancelled
- Failed

### 6.6 Logging and diagnostics

Logs are written to timestamped files under `logs`. Each entry includes the job name, operation, time, severity, and safe diagnostic context. Tokens, passwords, and secrets are redacted. The UI shows a readable summary and provides access to the full Log.

## 7. Portable file layout

```text
RcloneTransferManager/
├─ RcloneTransferManager.exe
├─ rclone.exe
├─ data/
│  ├─ jobs.json
│  ├─ app-settings.json
│  └─ rclone.conf
├─ logs/
└─ README.txt
```

On first run, the app creates missing data and Log files. `rclone.conf` starts empty and is populated only after the user authenticates. The app never packages the user's populated data directory into a release ZIP.

## 8. Error handling and safety

Validation occurs before any transfer begins:

- Link or path syntax
- Provider detection
- Authentication state
- Read permission on source
- Write permission on destination
- Local folder existence
- Same-source/destination detection
- Sync change totals

Errors use plain English, identify the affected field or operation, and provide a next action. Examples include `Authentication required`, `Folder not found`, `Destination is read-only`, `Network connection lost`, and `Not enough local disk space`.

The WPF application registers a global UI-thread exception handler that records a safe diagnostic and shows a recoverable error message instead of allowing the default crash dialog to appear.

## 9. Accessibility and quality requirements

- Every interactive control has a visible label or an automation name.
- Full primary workflow is keyboard accessible with logical tab order.
- Focus states remain visible.
- Normal text meets at least 4.5:1 contrast.
- Error state uses text plus icon/label, not color alone.
- Progress and completion states are announced in text.
- Layout is PerMonitorV2 DPI aware for Windows 10/11 scaling.
- Buttons have comfortable hit areas and do not rely on hover-only behavior.

## 10. Testing strategy

### Unit tests

- Local path validation
- Google Drive and OneDrive URL parsing
- Provider detection
- Saved-job serialization
- State transitions
- Log redaction
- Copy conflict decisions

### Integration tests

- rclone process launch with the bundled binary
- Dry-run/preview parsing
- Copy and Sync result mapping
- Cancel and Resume behavior
- Authentication-required and permission-denied flows
- Network failure and retry/resume behavior

### Manual acceptance tests

- Extract the ZIP on a clean Windows 10 machine and launch without installing .NET.
- Repeat on Windows 11 with display scaling above 100%.
- Login to Google Drive and OneDrive separately.
- Run local-to-local, local-to-Google Drive, Google Drive-to-OneDrive, and OneDrive-to-local Copy jobs.
- Verify changed-file conflict prompts.
- Verify Sync Preview blocks deletion until confirmation.
- Pause, resume, cancel, and inspect the Log for a large transfer.
- Confirm that the release ZIP contains no credentials.

## 11. Acceptance criteria for v1.0.0

The design is complete when a user can:

1. Extract the ZIP and open the program on Windows 10/11 x64.
2. Connect Google Drive or OneDrive through a browser OAuth flow.
3. Paste cloud links or enter/browse local paths.
4. Save and reload a transfer job.
5. Run a safe Copy with default settings.
6. Review and confirm a Sync before any deletion.
7. Resolve changed-file Copy conflicts one file at a time.
8. Monitor, pause, resume, cancel, and inspect a transfer.
9. Understand failures through the English UI and Log.
10. See version `1.0.0` and creator `Arkie'z K. Khositkhanawut` in the product metadata and About view.

## 12. Future-compatible extensions

The data model and provider layer should allow future additions without changing the main transfer workflow:

- Multiple accounts per provider
- Two-way Bisync
- Scheduled jobs
- Additional cloud providers
- Optional dark/system theme
- Optional installer
