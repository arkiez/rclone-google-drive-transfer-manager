# Remove Saved Jobs, Add Location Modes, and Adopt a Monochrome Theme

Date: 2026-08-06
Status: Approved design; pending implementation

## 1. Context

Rclone Transfer Manager currently exposes a saved-job workflow on the main
window. Users must enter a job name, can save and reload transfer settings,
and the application persists those settings in `data/jobs.json`.

The revised workflow removes saved jobs and makes each transfer an immediate,
one-time action. It also adds an explicit Cloud or Local choice for each
location and changes the application to a black-and-white visual theme with
semantic colors reserved for status communication.

## 2. Goals

- Remove all user-facing saved-job controls and persistence.
- Delete the legacy `data/jobs.json` file when the application starts.
- Keep Copy, Sync, preview, conflict handling, authentication, pause, resume,
  cancel, and logging behavior intact.
- Add independent Cloud and Local selectors for Source and Destination.
- Show an appropriate placeholder and Browse-button state for each selected
  location type.
- Validate entered locations against their selected types before transfer.
- Apply a monochrome theme across every application window while retaining
  green, orange, and red for success, warning, and error states.
- Show one completion popup immediately after each successful transfer.

## 3. Non-goals

- Do not remove the internal `TransferJob` type used to pass transfer details
  through the existing transfer pipeline.
- Do not change rclone command construction or transfer semantics.
- Do not change OAuth configuration or delete `data/rclone.conf`.
- Do not add scheduled transfers, transfer history, or a replacement preset
  system.
- Do not add a dark theme or a theme switcher.

## 4. Main-window design

Remove the Saved job dropdown, Refresh button, Job name field, and Save Job
button. The Transfer setup card becomes the first content section. The primary
workflow contains Source, Destination, Mode, and Start.

Source and Destination each receive a two-option segmented selector labelled
`Cloud` and `Local`. The selectors are independent so every supported route
remains possible, including Local to Cloud, Cloud to Local, Cloud to Cloud,
and Local to Local. Both selectors default to Local.

The selected segment uses a black background with white text. The unselected
segment uses a white background, dark text, and a dark border. The controls
remain keyboard accessible and expose meaningful automation names.

Changing a location type clears that location's current text and status. This
prevents a path or link entered under one mode from being silently interpreted
under the other mode.

## 5. Placeholders and Browse behavior

WPF `TextBox` does not provide a native placeholder. Each location input will
use a lightweight watermark overlay without adding a third-party dependency.
The watermark is visible only while the text box is empty and is also exposed
through accessibility help text.

The exact prompts are:

- Cloud Source: `Paste a Google Drive or OneDrive file or folder link`
- Cloud Destination: `Paste a Google Drive or OneDrive folder link`
- Local Source or Destination: `Enter a local folder path or click Browse`

Browse is visible and enabled in Local mode. It is hidden in Cloud mode.

## 6. Validation and transfer data flow

The existing `LocationResolver` remains the source of truth for interpreting
entered values.

Before a transfer starts:

1. Source and Destination must both be present.
2. A Local selection must resolve to `LocationKind.Local`.
3. A Cloud Source may resolve to a connected cloud location or a supported
   public direct-file link.
4. A Cloud Destination must resolve to a cloud location; a public direct-file
   link remains invalid as a destination.
5. Existing cross-location safety checks remain in force. In particular, a
   public direct-file source supports Copy to a local destination only.

Once validation succeeds, the main window creates an ephemeral `TransferJob`
in memory. Its name is generated as `Transfer yyyyMMdd-HHmmss`; its ID remains
a new GUID. The object is used by preview, conflict, transfer, monitor, and log
services but is never persisted.

The monitor and log describe the run as a transfer rather than asking the user
to manage a named job. Completion status is reported directly from the monitor
result and is not written to saved-job metadata.

## 7. Legacy data removal

`JobStore` and all calls that load or save jobs are removed. The public
`AppPaths.JobsFile` persistence path is removed as well.

Startup performs an idempotent cleanup of the exact legacy path
`data/jobs.json` after ensuring the application data directory exists:

- If the file exists, delete it.
- If it does not exist, do nothing.
- If deletion fails, write a diagnostic log, show a concise warning, and
  continue opening the application because the file is no longer read.

No other file in `data` is deleted or modified by this cleanup.

## 8. Theme design

Application-level resources provide the monochrome palette:

- White for primary surfaces.
- Very light neutral gray for the application background and neutral notices.
- Near-black for primary text, icons, selected segments, and the Start button.
- Medium gray for secondary text and disabled controls.
- Neutral gray for borders and separators.

The Start button has a black background and white content. Secondary buttons
have a white background, dark content, and a neutral border. Hover, pressed,
disabled, and keyboard-focus states remain visibly distinct.

Blue accents and hard-coded blue information panels are replaced with neutral
black, white, and gray resources in Main, Accounts, Conflict, Sync Preview, and
Transfer Monitor windows. Existing green, orange, and red resources remain
only for success, warning, and error meaning. Information-only notices use a
neutral treatment.

## 9. Successful-transfer popup

After rclone exits with code `0` and the transfer was not cancelled, the
Transfer Monitor shows an owner-bound modal message box immediately. Its title
is `Transfer completed`, its message is `The transfer finished successfully.`,
and it uses the Information icon with an OK button.

The popup appears exactly once per transfer, including a transfer that was
paused and resumed. Dismissing it leaves the completed Transfer Monitor open so
the user can inspect the result and activity log before choosing Close.

Failed and cancelled transfers do not show the success popup. They continue to
use the monitor's existing failure or cancellation state. This design does not
add a Windows toast dependency or a background notification service.

## 10. Components affected

- `MainWindow.xaml`: remove job controls; add selectors, watermarks, and revised
  layout.
- `MainWindow.xaml.cs`: remove persistence handlers; manage selector state;
  validate selected location types; create ephemeral transfers.
- `App.xaml`: define the monochrome palette and reusable segmented-selector and
  watermark presentation.
- `App.xaml.cs` and `AppPaths.cs`: perform targeted legacy-file cleanup.
- `TransferModels.cs`: retain `TransferJob` but remove persistence-only metadata
  when it has no remaining consumer.
- `TransferMonitorWindow`: use transfer wording for the generated run name and
  show the owner-bound success popup once after successful completion.
- Other window XAML files: replace blue information styling with neutral theme
  resources while preserving semantic status colors.
- `JobStore.cs`: delete the unused persistence service.
- User documentation: remove saved-job instructions and the `jobs.json` data
  entry; document Cloud and Local selection.

## 11. Error handling

- Empty inputs and selected-type mismatches are shown in the existing main
  validation area and prevent Start.
- Provider login and permission failures continue through the existing account
  and transfer error flows.
- A failure to delete the legacy jobs file is non-fatal but visible and logged.
- Clearing an input after a location-type change also clears stale validation
  status for that input.
- Failed and cancelled transfers never show a misleading success popup.

## 12. Verification

Implementation is complete when all of the following pass:

1. Build the WPF project in Release configuration.
2. Run the existing local Copy/Sync smoke test.
3. Confirm no active source reference remains to `JobStore`, saved-job event
   handlers, or job persistence.
4. Start with a test `data/jobs.json` and confirm only that file is deleted;
   `rclone.conf` and logs remain intact.
5. Verify Source and Destination selectors independently update placeholders,
   clear stale input, and toggle Browse visibility.
6. Verify Local/Local, Local/Cloud, Cloud/Local, and Cloud/Cloud validation.
7. Verify a public direct-file source is accepted as Cloud Source but rejected
   as a destination and continues to require Copy to Local.
8. Inspect Main, Accounts, Conflict, Sync Preview, and Transfer Monitor windows
   for consistent monochrome styling, visible focus, and semantic status colors.
9. Confirm a transfer can start without a user-entered name and that monitor
   and log output use the generated transfer name.
10. Confirm a successful transfer shows the approved popup exactly once and
    that dismissing it leaves the completed monitor open.
11. Confirm failed and cancelled transfers do not show the success popup.

## 13. Acceptance criteria

- No saved-job or job-name control remains in the main workflow.
- No transfer setup is read from or written to `jobs.json`.
- The exact legacy `data/jobs.json` file is removed on startup when possible.
- Cloud and Local are independently selectable for Source and Destination.
- Each selection has the approved placeholder, Browse behavior, and strict
  type validation.
- All application windows use the approved monochrome base theme.
- Every successful transfer shows one immediate, owner-bound completion popup;
  failed and cancelled transfers do not.
- Copy and Sync safety behavior remains unchanged.
