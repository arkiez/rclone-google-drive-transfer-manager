# Rclone Transfer Manager — Compact UI and Icon Design

Date: 2026-08-06  
Status: Approved in conversation; implementation pending  
Product: Rclone Transfer Manager v1.0.0  
Creator: Arkie'z K. Khositkhanawut

## Goal

Make the existing WPF desktop utility more compact and scannable without
removing any transfer capability. Add a consistent vector icon language while
keeping visible text labels, keyboard access, and screen-reader names.

## Recommended approach

Use a compact form-first layout. The main window remains a single transfer
screen, rather than adding a sidebar or multi-step wizard. This preserves the
current beginner-friendly flow while reducing vertical scrolling and repeated
card padding.

## Main window

- Use a compact 900 x 660 default window with a 680 x 560 minimum.
- Keep a slim header with product identity, Accounts, and About.
- Put Saved job and Job name in one compact row.
- Put Source and Destination in a two-column route section at normal desktop
  widths; each field keeps its label, Browse action, provider status, and
  AutomationProperties name.
- Keep Copy and Sync beside the action buttons in a compact action row.
- Keep the safety tip as a single short information strip.
- Keep the footer version and creator text, with reduced vertical padding.
- Preserve scrolling as a fallback for Windows scaling or narrow windows.

## Visual system

Keep the existing light professional tokens:

- Primary: #2563EB
- Background: #F8FAFC
- Foreground: #0F172A
- Muted surface: #F1F5FD
- Border: #E4ECFC
- Warning: #D97706
- Destructive: #DC2626

Use an 8 px spacing rhythm, smaller card padding, 36 px text fields, and
compact 34–36 px buttons. Keep Segoe UI for text and Consolas for paths/logs.
Avoid gradients, emoji, decorative shadows, and excessive rounded containers.

## Icons

Use WPF vector Path geometries from one consistent outline family. Define
reusable icon resources in App.xaml with a standard 16 px button icon size
and 18 px section/icon size.

Required labeled icons:

- Accounts: cloud/person
- About: information
- Refresh: circular arrow
- Browse: folder
- Save Job: floppy/save mark
- Start Transfer: play/arrow
- Copy: copy/file mark
- Sync: two-way arrows
- Warning/tip: triangle with exclamation

Buttons keep text labels next to icons. Icon-only controls, if any, must have
tooltips and AutomationProperties.Name; icons must never be the only state
indicator.

## Secondary windows

Apply the same compact header, spacing, icon sizing, and button treatment to
Accounts, Conflict Review, Sync Preview, and Transfer Monitor. Preserve their
existing safety messages and explicit action labels.

## Accessibility and behavior

- No transfer, auth, save-job, preview, conflict, pause, resume, or cancel
  behavior changes.
- Preserve logical keyboard tab order and visible focus states.
- Preserve all existing AutomationProperties.Name values and add names for
  any new icon-bearing controls.
- Keep state communicated with text plus semantic icon/color, never color alone.
- Do not introduce motion that is required to understand state.

## Verification

1. Build Release with zero warnings and errors.
2. Launch the compact EXE and verify the title and startup window.
3. Run the existing local Copy/Sync smoke test.
4. Confirm the main screen is usable without scrolling at the compact target
   size and remains accessible through the fallback ScrollViewer.
5. Inspect the organized ZIP for the compact single-file layout, empty
   data/ and logs/, and no credential files.
