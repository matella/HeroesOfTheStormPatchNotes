# Verification evidence — issue #1

## Local quality gates

- `cargo fmt --check`: passed.
- `cargo clippy --locked --all-targets -- -D warnings`: passed.
- `cargo test --locked`: 22 passed.
- `python3 -m unittest discover -s scripts -p 'test_*.py'`: 6 passed.
- `git diff --check` and staged equivalent: passed.
- Independent GADS Skeptic and Reviewer: see review-issue-1.md.
- Dependency metadata: 249 resolved registry packages, all with license declarations;
  versions/SPDX expressions are recorded in dependency-licenses.json. This is not
  a vulnerability scan or a blanket license grant for third-party game content.

## Live data verification

Complete generation has succeeded against the pinned live build 2.57.0.98348:
91 heroes, 35 battlegrounds, all twelve locales with zero required hero/talent/map
text gaps, and 1,230 bundled shared assets. JSON Schema validation passed for game,
patches, assets, manifest, and all twelve locale documents. Per-map native patches
are applied against reconstructed exported base documents during validation.

Cross-source reconciliation produces 276 release records including one latest PTR
candidate; unavailable/unapproved historical bodies remain explicitly metadata-only.
The exact final availability counts and dataset/archive hashes are in the generated
manifest, rather than hardcoded as a universal target. The old suite's 608-row
count includes multiple representations and PTR history and is not a unique-patch
coverage target.

The full live generation was repeated: the second pass printed `unchanged` and
preserved the existing semantic dataset version. Fixtures independently establish
byte-identical archives from identical data despite filesystem timestamp changes.
Publication tests simulate interrupted upload, provenance-only retry, stale version
claims, tampering, and unchanged input without promoting a partial release.

## Limits and release gate

No GitHub Actions production run, release publication, merge, or deployed API change
was performed during local verification. A PR run verifies the quality workflow;
a manual artifact-only run and first reviewed release verify the hosting pipeline.

Patch article discovery is US English; twelve game-data locales do not imply twelve
article translations. PTR cycle association can be inferred and is labeled candidate.
The source registry keeps uncertain community-content reuse disabled, preserving
links/metadata instead. Core assets are loading portraits, map loading art and
hero ability/talent icons, not every cosmetic in the upstream image repository.

Legacy code can be recovered from commit 015c3776940364d5643af29fbf8cb8fddad73c4d.
Rollback a published dataset by promoting a previous fully validated immutable
release. Do not delete the currently working latest asset before replacement.

## Final generated artifact

- Dataset version: `bf6c543f34733ad61811f6b4dc635908892e2949b9284f683e577cd3d4c9c0c9`.
- Game version: `2.57.0.98348`.
- Counts: {"battlegrounds": 35, "heroes": 91, "metadataOnlyPatches": 26, "patches": 276}.
- Bundled assets: 1,230. All twelve locale coverage gaps: zero.
- Archive SHA-256: `245bb9bcf87f2ef9d8f1af270df3afec8353d88a9f460a7bb85c96022b1a1b14`.
- All 16 generated JSON documents pass their published JSON Schemas.
- `scripts/releases.py::verify_directory` passed against the complete live package.
