# Preserve Empty Folders and Cache Cloud Connection Checks

## Context

Field use found two workflow problems in the Rclone Transfer Manager:

1. Copying a source tree with an empty folder does not create that folder at the destination.
2. Every Copy attempt performs a fresh provider connection check, even after the provider was already verified during the current application session.

The current transfer command builder does not pass rclone's `--create-empty-src-dirs` flag. The current `MainWindow` workflow calls `RcloneConfigService.IsConnectedAsync` for every cloud provider on every transfer, and that method runs `rclone lsd` each time.

## Goals

- Preserve empty source directories during Copy operations.
- Verify each cloud provider at most once per application session unless the user explicitly refreshes account status or changes the connection.
- Keep the existing login rules: local transfers remain login-free, public direct-file downloads remain login-free, and cloud locations still require the relevant provider connection.
- Keep explicit account refresh reliable and make Connect/Disconnect immediately affect the cached state.
- Keep Sync behavior unchanged except for sharing the same connection cache.

## Non-goals

- Do not add anonymous recursive copying for public cloud folders.
- Do not change provider detection, OAuth flow, conflict handling, or Sync deletion behavior.
- Do not persist the connection cache between application launches.
- Do not add a new test framework to this small portable application.

## Design

### Empty-directory preservation

`TransferService.BuildArguments` will add `--create-empty-src-dirs` when `TransferMode.Copy` is selected. Because the same argument builder is used for Copy preview and the real transfer, the flag will be present in both paths. It will not be added to the public direct-file `copyurl` path, which has no source directory tree.

The flag preserves empty directories below the selected source root. The source root itself continues to follow rclone's existing contents-of-directory semantics.

### Session-scoped connection cache

`RcloneConfigService` will own a private in-memory set of verified cloud providers. The cache is keyed by `LocationKind` and is initialized empty for every application process.

`IsConnectedAsync` will accept an optional `forceRefresh` argument, defaulting to `false`:

1. If the provider remote is absent from the local rclone config, remove any cached entry and return `false`.
2. If `forceRefresh` is false and the provider is cached, return `true` without starting rclone.
3. Otherwise run the existing `rclone lsd` check.
4. Add the provider to the cache only when the check succeeds; remove it when the check fails.

`ConnectAsync` will add a provider to the cache after a successful OAuth/config operation. `DisconnectAsync` will always remove it after a successful disconnect.

The main transfer flow will continue to call `IsConnectedAsync` with the default behavior. The first transfer requiring a provider therefore performs the normal check; later transfers in the same session reuse the verified result. `AccountsWindow.RefreshAsync` will pass `forceRefresh: true`, so the Refresh button and initial account-window status remain authoritative. The automatic login flow will re-check after the Accounts window closes using the updated cache state.

### Error handling

- A missing remote remains a login-required condition and never becomes a cache hit.
- A failed or canceled OAuth flow keeps the provider unverified and blocks the transfer as it does today.
- An explicit Disconnect invalidates the provider immediately.
- A new application process starts with an empty cache, so credentials are verified again after restart.
- If a cached credential later expires, rclone's transfer error remains the source of truth; the user can use Accounts > Refresh or reconnect to invalidate and revalidate the cache.

## Verification

Automated and manual verification will cover:

- The existing release build and smoke tests.
- A local Copy smoke case with a nested empty source directory; the destination must contain the empty directory after Copy.
- Copy preview and real Copy command construction both include `--create-empty-src-dirs`.
- A provider's first session check invokes the connection probe, while a second check uses the cache.
- `forceRefresh` bypasses the cache and updates it from the probe result.
- Successful Connect caches the provider, and successful Disconnect removes it.
- Local-to-local and public-file-to-local transfers do not trigger provider checks.

## Acceptance criteria

- Copying a source folder containing empty subfolders reproduces the same empty subfolder structure at the destination.
- After a cloud provider has been verified once in the current app session, pressing Copy again does not run another connection probe for that provider.
- Accounts Refresh still performs a fresh provider check.
- Connect and Disconnect correctly change whether the next transfer requires a check/login.
- Existing provider login, public-file, conflict, Sync, logging, and packaging behavior remains intact.
