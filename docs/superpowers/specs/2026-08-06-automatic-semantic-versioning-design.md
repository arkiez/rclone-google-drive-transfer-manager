# Central Semantic Versioning Design

Date: 2026-08-06
Status: Implemented and verified

## Goal

Keep the program version consistent whenever code changes without adding Git
hooks, commit wrappers, bots, or CI version automation.

The repository will have one canonical version source. A developer or agent
must choose and apply the correct semantic-version increment before committing
and pushing a code change.

## Version Policy

Use the standard three-part format `major.minor.patch`.

- Increment `major` for a breaking or incompatible change, then reset minor and
  patch to zero.
- Increment `minor` for a backward-compatible feature, then reset patch to
  zero.
- Increment `patch` for a backward-compatible bug fix or small behavior
  correction.
- Do not change the version for documentation-only edits.

Examples from `1.1.1`:

```text
Breaking change: 2.0.0
New feature:     1.2.0
Bug fix:        1.1.2
Docs only:      1.1.1
```

Changes under `src/` and behavior-changing changes under `scripts/` require a
version decision. The decision and version update must be part of the same
commit as the code change and must be present before push.

The implementation of this design adds a backward-compatible repository
feature, so the product version changes from `1.1.1` to `1.2.0`.

## Canonical Version Source

Add a repository-root `Version.props` file as the only product-version source.
It contains the canonical `Version`, `FileVersion`, and `AssemblyVersion`
properties used by MSBuild.

`RcloneTransferManager.csproj` imports `Version.props` and no longer contains
hard-coded release-version properties.

All visible or generated version values derive from the built assembly or
`Version.props`:

- Main-window title and footer use the assembly version at runtime.
- About dialog and logs use the assembly version at runtime.
- `scripts/package.ps1` reads `Version.props` to construct release and staging
  ZIP names.
- The packaged README receives the canonical version during packaging.
- Repository documentation avoids maintaining an independent numeric version.
- The application manifest identity remains a static technical identity and is
  not presented as the product version.

## Repository Requirement

Add a root `AGENTS.md` rule requiring agents and contributors to update
`Version.props` whenever they commit code changes:

```text
Breaking change -> major
New feature     -> minor
Bug fix         -> patch
Docs only       -> no bump
```

Document the same concise rule in the README. No Git hook enforces the rule;
code review and the contributor or agent performing the commit are responsible
for compliance.

## Error Handling

Build and packaging must fail clearly when `Version.props` is missing or when
the canonical version is not exactly three non-negative integer components.
Packaging must never fall back to `1.1.1`, infer a version from an existing ZIP,
or silently use a stale hard-coded name.

## Verification

Implementation is complete when all of the following pass:

- `Version.props` reports `1.2.0`, `1.2.0.0`, and `1.2.0.0` for product, file,
  and assembly versions.
- Release build completes without warnings or errors.
- Main-window title, footer, About dialog, and logs show `1.2.0`.
- The built EXE file metadata reports `1.2.0.0`.
- Package creation produces `RcloneTransferManager-v1.2.0-win-x64.zip`.
- The packaged README reports version `1.2.0`.
- No active source, script, or repository documentation contains a hard-coded
  `1.1.1` product version.
- Existing development and packaged-backend smoke tests pass.
- The final code commit includes the `1.2.0` version update.

## Excluded Complexity

This design deliberately excludes:

- Git hooks.
- Commit or push wrapper commands.
- GitHub Actions version checks.
- Automatic commits, tags, releases, or pushes.
- Version calculation from commit messages.

These mechanisms can be reconsidered later only if manual compliance becomes a
real maintenance problem.
