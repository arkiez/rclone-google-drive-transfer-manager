# Portable Package with Internal rclone Design

Date: 2026-08-06
Status: Approved for implementation

## Goal

Make the portable release unambiguous to non-technical users. After extracting
the ZIP, the package root must contain only one executable that the user can
open: `RcloneTransferManager.exe`.

The bundled rclone executable remains part of the product because it is the
transfer backend, but it moves into a clearly internal directory and is started
only by Rclone Transfer Manager.

## Current Problem

The current publish output places both `RcloneTransferManager.exe` and
`rclone.exe` in the package root. It also leaves several native WPF DLLs beside
them. Recipients can reasonably mistake `rclone.exe` for the program they are
supposed to open.

## Approved Package Layout

The extracted portable package will have this top-level structure:

```text
RcloneTransferManager.exe
README.txt
_internal/
  rclone.exe
data/
logs/
```

Rules:

- `RcloneTransferManager.exe` is the only `.exe` in the package root.
- `_internal/rclone.exe` is required and is not a user entry point.
- `data` and `logs` stay in the package root to preserve the existing portable
  data model.
- Native .NET/WPF libraries are bundled into the main single-file executable
  instead of being left as loose root-level DLLs.
- The package remains a ZIP that runs after extraction and requires no
  installer or administrator access.

## Build and Project Integration

`RcloneTransferManager.csproj` will link the repository's pinned `rclone.exe`
into the publish output as `_internal/rclone.exe`. The backend remains excluded
from .NET single-file embedding so it can be launched as a separate process.

The package script will publish a self-contained `win-x64` application with
single-file publishing enabled and native libraries included in the
self-extracting application bundle. It will then add `README.txt`, create the
empty portable `data` and `logs` directories, and compress the validated output.

The approved RT program icon is a separate asset change, but it will be embedded
in the same `RcloneTransferManager.exe`. Together, the icon and one-executable
root make the intended entry point immediately recognizable.

## Runtime Path and Data Flow

`AppPaths.Root` continues to resolve to the directory containing
`RcloneTransferManager.exe`.

Runtime paths become:

```text
Backend: <Root>/_internal/rclone.exe
Config:  <Root>/data/rclone.conf
Logs:    <Root>/logs/
```

`RcloneProcessRunner` continues to launch rclone without a console window and
uses the package root as its working directory. Only the executable path
changes. Command arguments, configuration handling, cancellation, and transfer
behavior remain unchanged.

Existing user data is not migrated because the locations of `data`, `logs`, and
`rclone.conf` do not change.

## Validation and Failure Handling

Before creating the ZIP, the package script must verify all of the following:

- The publish root contains exactly one `.exe`, named
  `RcloneTransferManager.exe`.
- No loose native WPF DLLs remain in the publish root.
- `_internal/rclone.exe` exists.
- `README.txt`, `data`, and `logs` exist.

Packaging fails with a clear error if any invariant is violated. A broken
package must never be emitted silently.

At runtime, a missing backend produces a user-facing error that identifies it
as an internal required component and advises the user to extract the complete
ZIP again or check antivirus quarantine. The message must not suggest opening
`rclone.exe` directly.

## Documentation

The root README and packaged user guide will state:

- Open `RcloneTransferManager.exe`.
- Do not move or edit files inside `_internal`.
- If the backend is reported missing, re-extract the full package and check
  antivirus quarantine.

The documentation will not instruct recipients to run rclone from a terminal.

## Verification

Implementation is complete when all of the following pass:

- Release build completes without warnings or errors.
- Package creation succeeds and produces the expected ZIP.
- Listing the ZIP confirms exactly one root-level executable.
- `_internal/rclone.exe` runs when invoked by the application.
- No loose native WPF DLLs appear in the package root.
- Development smoke testing still works with the repository backend.
- Package-level smoke testing explicitly exercises the packaged internal
  backend path.
- The packaged application starts successfully after extraction.
- A real local-to-local transfer completes through the packaged application.
- A deliberate missing-backend test produces the approved actionable error.
- Existing portable configuration and logs remain under `data` and `logs`.

## Rejected Alternatives

### Embed and extract rclone at application startup

This could remove the visible `_internal` directory, but it adds extraction,
versioning, cleanup, and antivirus risks without improving transfer behavior.

### Keep both executables in the root and add a shortcut

This is simpler to implement but does not remove the ambiguous second program
and makes the portable package dependent on a shortcut that may be moved or
discarded.
