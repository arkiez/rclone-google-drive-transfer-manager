# Automatic Semantic Versioning Design

Date: 2026-08-06
Status: Approved for implementation

## Goal

Make semantic version updates an enforced part of the repository's commit and
push workflow. A product version change must be derived from the final
Conventional Commit message, included in the same commit as the product change,
and verified again before push and in GitHub CI.

Editing or saving a file does not change the version. Committing is the version
boundary.

## Version Policy

The repository requires these mappings:

- `feat:` increments the minor version and resets patch to zero.
- `fix:` increments the patch version.
- A header with `!` before the colon, or a `BREAKING CHANGE:` footer,
  increments the major version and resets minor and patch to zero.
- `docs:` and `chore:` do not change the version.
- Other Conventional Commit types do not change the version unless this policy
  is extended in a later approved change.
- A breaking marker always takes precedence over the ordinary commit type.
- A commit header that does not follow Conventional Commit syntax is rejected.

Examples from version `1.1.1`:

```text
feat: add automatic updates       -> 1.2.0
fix: correct package validation   -> 1.1.2
feat!: replace package format     -> 2.0.0
docs: update release guide        -> 1.1.1
```

The implementation of this design is itself a `feat:` and therefore changes
the current product version from `1.1.1` to `1.2.0`.

## Canonical Version Source

Add a repository-root `Version.props` file as the single source of truth for
the three-part product version. The WPF project imports this file for assembly,
file, and informational version metadata.

Remove hard-coded release versions where practical:

- Window title, footer, About dialog, and logs read the built assembly version.
- `scripts/package.ps1` reads `Version.props` and derives the release and
  staging ZIP names.
- Documentation version mirrors are synchronized by the version script and
  verified against `Version.props`.
- The application manifest identity is synchronized by the version script and
  remains a generated mirror rather than an independent product-version source.

The version automation must never derive the product version from a ZIP name,
XAML string, README, Git tag, or existing build output.

## Version Script

Add `scripts/version.ps1` as the reusable implementation behind local hooks and
CI. It provides isolated commands for:

- Reading and validating the canonical version.
- Classifying a Conventional Commit message as major, minor, patch, or no-bump.
- Calculating the next semantic version.
- Applying and staging the canonical version and generated mirrors.
- Verifying that repository version references are synchronized.
- Verifying the version transition between two Git commits.

The script must be deterministic and idempotent. Re-running it for the same
commit operation, including an amend flow, must not increment the version a
second time.

Only version-owned files may be modified or staged by the script. Existing
unrelated staged and unstaged user changes must be preserved.

## Local Git Hooks

Store hooks under `.githooks` so their behavior is version controlled.

### Commit hook

The commit-message hook reads the final message, calculates the required bump,
updates version-owned files, and stages those files into the same commit. It
does not create a second version-only commit.

For no-bump commit types, the hook verifies synchronization but leaves the
version unchanged. A nonconforming commit message, malformed canonical version,
unsupported version shape, or failure to update every generated mirror aborts
the commit with an actionable message.

Amending a commit must preserve the already-required version unless the commit
type or breaking intent changes. Tests must prove that a repeated hook run does
not double-bump.

### Pre-push hook

The pre-push hook is read-only. It examines outgoing commits in chronological
order and checks each non-merge commit's version transition against its message.
It also verifies the checked-out version mirrors.

The hook blocks push when:

- A required bump is missing or uses the wrong semantic component.
- A no-bump commit changes the product version.
- Canonical and generated versions disagree.
- A version is malformed or moves backward.

The pre-push hook must never edit files, create commits, amend history, or
change refs while a push is in progress.

## Hook Installation

Add `scripts/install-git-hooks.ps1` to set the current repository's
`core.hooksPath` to `.githooks` and verify that the expected hooks are present.
Run it during implementation for the current checkout.

Because Git does not propagate `core.hooksPath` through clone, the README must
state that contributors run the installer once after cloning. The installer is
safe to rerun and reports the active hooks path.

## GitHub Enforcement

Add a GitHub Actions workflow for pushes and pull requests. CI runs the same
version verification script rather than reimplementing semantic rules in YAML.

CI is required because local hooks can be bypassed with `--no-verify`. CI does
not bump versions, push bot commits, rewrite contributor branches, publish a
release, or generate tags. Its sole responsibility in this feature is to reject
invalid version history and unsynchronized version references.

## Error Handling

Failures must identify:

- The commit being checked.
- Its detected bump category.
- The previous, expected, and actual versions.
- The command the contributor can run to repair or verify the state.

Hook failures return a non-zero exit code. No failure path silently selects a
default version or falls back to the old hard-coded `1.1.1` value.

## Verification

Implementation is complete when all of the following pass:

- Parser tests cover `feat`, `fix`, breaking headers, breaking footers,
  `docs`, `chore`, scopes, malformed headers, and multiline messages.
- Calculation tests cover major, minor, patch, reset behavior, and malformed
  canonical versions.
- Temporary-repository integration tests prove same-commit staging, no-bump
  behavior, idempotence, amend behavior, and preservation of unrelated changes.
- Pre-push integration tests accept valid outgoing history and reject missing,
  incorrect, backward, and no-bump version changes.
- Local hook installation is idempotent and sets `.githooks` for this checkout.
- GitHub workflow syntax is valid and invokes the shared verifier.
- Release build completes without warnings or errors at version `1.2.0`.
- Application UI, About dialog, logs, file metadata, packaged README, and ZIP
  filename report `1.2.0` consistently.
- Package creation succeeds as
  `RcloneTransferManager-v1.2.0-win-x64.zip`.
- Existing transfer and package smoke tests continue to pass.

## Rejected Alternatives

### Commit-and-push wrapper only

A wrapper is straightforward but can be bypassed by ordinary `git commit` and
does not establish a repository-wide requirement.

### GitHub bot bump after push

A bot-created version commit separates code from its version, leaves local
branches behind the remote, creates loop and permission concerns, and makes a
push mutate history after contributor review.

### Bump during pre-push

Changing refs or creating a commit after push negotiation is unsafe and can
leave the version commit unpushed. Pre-push is validation-only by design.
