# Rclone Transfer Manager: Public Link Authentication

Date: 2026-08-06
Status: Design approved in conversation; written-spec review pending

## Context

Rclone Transfer Manager currently resolves Google Drive and OneDrive folder
links to provider remotes. Any cloud location then requires a configured
OAuth remote before Copy or Sync can start. This is correct for folders shared
with a user, but it is too strict for a public, direct-download file URL whose
content can be fetched anonymously into a local folder.

The URL itself cannot reliably prove whether an item is public, user-shared,
or a folder. The application therefore needs a conservative classification:
only an explicit direct-file download URL may bypass OAuth. Provider folder
links and ambiguous share links continue through the authenticated cloud
path.

## Goals

- Allow a public direct file URL to be copied to a local Windows folder
  without logging in.
- Require OAuth for public folder links, user-shared links, and any cloud
  destination.
- Automatically start the existing provider OAuth flow for missing accounts
  when a transfer needs them.
- Keep local-to-local transfers login-free.
- Apply the same authentication rules to Copy and Sync, while restricting the
  anonymous direct-file path to Copy because Syncing one URL must not delete
  unrelated files in a local destination.
- Keep OAuth tokens and public-link secrets out of activity logs.
- Preserve the existing conflict and Sync preview safety behavior.

## Non-goals

- Anonymous recursive listing or copying of public Google Drive/OneDrive
  folders.
- Anonymous upload to a cloud provider.
- Bypassing permissions on a link that is not actually public.
- Supporting provider-specific password-protected links in the first pass.

## User-visible behavior

Authentication is determined by the resolved source and destination:

| Source | Destination | Authentication |
| --- | --- | --- |
| Local | Local | None |
| Public direct file URL | Local | None |
| Google Drive/OneDrive folder URL | Local | Source provider |
| User-shared/private cloud location | Local | Source provider |
| Local | Google Drive/OneDrive | Destination provider |
| Cloud | Cloud | Every referenced provider |

When a cloud provider is required but not connected, Start Transfer opens the
Accounts window and starts that provider's browser OAuth flow. If more than
one provider is required, the missing providers are handled one at a time.
Canceling or failing any required login stops the transfer before preview or
data changes.

After OAuth completes, the job is resolved again. A valid login without access
to the shared item produces a clear permission error and suggests asking the
owner to grant access or using the correct account.

## Location classification

LocationResolver will distinguish three relevant URL classes:

1. Local path: existing behavior.
2. Authenticated cloud location: Google Drive/OneDrive folder links, provider
   share links, rclone paths, and ambiguous URLs.
3. Public direct file URL: an explicit URL that is intended to return one file
   through HTTP. It is source-only and cannot be a cloud destination.

The direct-file classifier must be conservative. Known folder patterns such as
Google Drive /folders/<id> remain authenticated. URLs that only identify an
item but do not establish a direct download response remain authenticated or
are rejected with an explanation. The first implementation may accept
provider direct-download URLs and known file URL forms only when the download
path can be passed safely to rclone.

The direct-file transfer uses rclone copyurl with automatic filename and
HTTP-header filename support, writing into the selected local destination
folder. Rclone documents copyurl as downloading URL content to a destination
and supports --auto-filename and --header-filename.

References:

- https://rclone.org/commands/rclone_copyurl/
- https://rclone.org/drive/
- https://rclone.org/onedrive/

## Data flow

1. Source and destination text changes call the resolver and update compact
   status text:
   - Public file - No login required
   - <Provider> - Login required
   - <Provider> - Connected
   - Local folder
2. Start Transfer resolves both locations.
3. For each cloud location, ensure the corresponding rclone remote exists and
   is usable. Missing remotes invoke the existing Accounts/OAuth flow.
4. Re-resolve the job after login.
5. If the source is a public direct file, allow only Copy to a local
   destination and run the copyurl path.
6. Otherwise continue through the existing authenticated Copy conflict
   preview, Sync preview, and transfer monitor.
7. Convert provider/auth failures into actionable UI text without exposing
   tokens.

## Logging and credential safety

The job log must not write the full raw public URL because share URLs can
contain access-bearing query or path tokens. Logs should identify the
location as Public direct file, Google Drive, OneDrive, or Local and use a
redacted URL/path when diagnostics need a reference. OAuth tokens remain in
the per-machine rclone config and are never written to logs or packaged
release ZIPs.

## Error handling

- A public file URL used as a destination: reject before starting.
- A public file URL selected with Sync: reject and explain that anonymous
  single-file sources support Copy only.
- A public link that returns an HTML sign-in page, an expired share page, or a
  non-download response: report that it is not a usable public direct file
  link and offer the authenticated cloud flow.
- Missing account: open Accounts/OAuth automatically.
- Login canceled or failed: stop without starting rclone.
- Login succeeds but provider returns permission denied: explain that the
  connected account is not allowed to access the shared item.
- Local source/destination validation remains unchanged.

## Verification

Offline resolver and service tests must cover:

- local to local requires no login;
- public direct file to local requires no remote;
- cloud folder to local requires the source remote;
- local to cloud requires the destination remote;
- cloud to cloud requires all referenced remotes;
- public file cannot be a destination;
- public file cannot run in Sync mode;
- raw public URLs are redacted from logs.

Smoke coverage should include a local Copy, a local Sync, and the existing
combined preview parser. A manual check should confirm that a missing
provider opens the browser OAuth flow and that canceling it leaves the
transfer unapplied.

## Rollback

If a provider's public download response is not reliably detectable, disable
the anonymous direct-file classifier for that provider and leave its links on
the authenticated cloud path. This preserves the existing behavior and does
not weaken access control.
