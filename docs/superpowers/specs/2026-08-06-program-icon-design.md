# Rclone Transfer Manager Program Icon Design

Date: 2026-08-06
Status: Implemented and verified

## Goal

Add a distinctive program icon to Rclone Transfer Manager that matches the
application's approved monochrome visual theme and remains recognizable at
Windows shell sizes.

## Approved Direction

The user selected the `RT Monogram` direction (visual option C) and confirmed
that the final mark should keep the two letters `RT` shown in the preview.

The icon consists of:

- A near-black rounded-square tile.
- A bold white geometric `RT` monogram.
- Flat, high-contrast construction without gradients, shadows, textures, or
  additional colors.
- Generous internal spacing so the mark remains legible at 16 px.

The monogram represents Rclone Transfer Manager while keeping the compact icon
more readable than a three-letter `RTM` mark.

## Asset Strategy

Use a vector-first workflow:

1. Store an editable SVG master in the application's `Assets` directory.
2. Generate a multi-resolution Windows ICO from the same geometry.
3. Include standard shell sizes from 16 px through 256 px, with pixel-aware
   simplification or spacing adjustments where required.

The SVG is the maintainable source of truth. The ICO is the compiled Windows
asset used by the application. A single PNG or an ICO-only workflow is rejected
because either option makes future edits less reliable and can produce weaker
small-size rendering.

## WPF Integration

- Configure `ApplicationIcon` in `RcloneTransferManager.csproj` so the icon is
  embedded in `RcloneTransferManager.exe`.
- Make the icon available to WPF windows as an application resource.
- Ensure the main window and secondary windows display the same icon in their
  title bars and taskbar representations.
- The published executable should carry the icon without requiring a separate
  runtime asset beside the EXE.

Expected Windows surfaces include:

- File Explorer executable and shortcut views.
- Taskbar and window switcher.
- Start menu entries or shortcuts created by the user.
- Main and secondary WPF window title bars.

## Accessibility and Visual Constraints

- Preserve strong black/white contrast.
- Do not rely on color to identify the application.
- Keep the silhouette simple enough to distinguish at 16 px and 20 px.
- Avoid fine strokes, small counters, and tightly packed lettering.
- Retain adequate padding around the monogram at every generated size.

## Failure Handling

Icon generation should be reproducible from repository files. If generation
fails, the build must not silently substitute an unrelated icon. The checked-in
ICO remains the build input, while the SVG master documents the intended source
geometry.

## Verification

Implementation is complete when all of the following pass:

- Release build completes without warnings or errors.
- The generated ICO contains multiple Windows icon sizes, including 16, 32,
  48, and 256 px.
- The compiled EXE exposes the RT icon in Windows Explorer.
- The icon appears on the main window and all secondary window title bars.
- The icon remains visually legible at 16 px, 32 px, and 48 px.
- Existing transfer behavior and the previously implemented UI changes remain
  unaffected.
