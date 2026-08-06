# Repository Instructions

## Versioning

Before committing or pushing a code change, update the canonical version in
`Version.props` in the same commit:

- Breaking or incompatible change: increment major and reset minor/patch.
- Backward-compatible feature: increment minor and reset patch.
- Backward-compatible bug fix or small behavior correction: increment patch.
- Documentation-only change: do not change the version.

All visible versions and package names must continue to derive from
`Version.props`. Do not add independent hard-coded product versions.

Run `./scripts/version-test.ps1` before committing or pushing.
