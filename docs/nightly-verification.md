# Hosted nightly verification — 2026-11-12

The owner explicitly requested running the nightly workflow and publishing the
required releases. PR #2 was merged as
`993a594e122c1bc558f0cb8a2f94b355e54f2067` after its quality checks passed.
The post-merge Rust quality run also passed.

## Observable plan

- [x] Run the complete workflow on `main` with `publish=true` and verify promotion.
- [x] Independently download public assets, verify hashes and archive contents,
  validate JSON Schemas, and run the Rust dataset validator.
- [x] Run the same workflow again with `publish=true`; verify an unchanged result
  creates no additional release and preserves the existing latest release.
- [x] Confirm the workflow is active on the default branch with the nightly cron.

## Schedule and review

The first run, [43308945943](https://github.com/matella/HeroesOfTheStormPatchNotes/actions/runs/43308945943),
successfully generated and validated the complete dataset, then uploaded all 24
release assets. Final publication verification failed because GitHub's tag lookup
did not expose the unpublished draft. The release remained a draft and no latest
release was promoted. The fix discovers drafts through the authenticated paginated
release list and performs final verification through the numeric release ID.
All 12 publication tests pass, including draft lookup, interrupted draft reuse,
and rejection when final release verification is unavailable.

The active schedule is `17 3 * * *` (03:17 UTC, 04:17 Brussels in winter and
05:17 in summer). GitHub schedules are best-effort. The checks below use manual
dispatch of the same workflow; a future cron event has not yet been observed.

**[Reviewer]** initially approved the workflow but missed the draft visibility
defect that the first real run exposed. A focused review approved the correction
and independently passed all 12 publication tests. Publication remains limited to
`main`, unchanged semantic data creates no release, and every staged asset is
verified before latest is promoted. Failed staging preserves the previous latest
release. Hosted verification of the correction is recorded below.

## Successful hosted publication

- Corrective [PR #3](https://github.com/matella/HeroesOfTheStormPatchNotes/pull/3)
  merged as `99df8f3b7e622126dea9c25aa1a1ffb0124e6e8e` after quality checks passed.
- [Run 43310518686](https://github.com/matella/HeroesOfTheStormPatchNotes/actions/runs/43310518686):
  build and publication both succeeded with `publish=true` on `main`.
- Published release ID: `443531351`;
  [immutable release](https://github.com/matella/HeroesOfTheStormPatchNotes/releases/tag/data-c8d8ff8f5357d9e8b644d416376bb541c3dd31ec3406f60c9f41f44037483bab).
- Dataset version: `bf6c543f34733ad61811f6b4dc635908892e2949b9284f683e577cd3d4c9c0c9`.
- Archive SHA-256: `c8d8ff8f5357d9e8b644d416376bb541c3dd31ec3406f60c9f41f44037483bab`.
- Game `2.57.0.98348`: 91 heroes, 35 battlegrounds, 12 locales, 1,230 shared
  assets and 276 patch records, including 26 explicitly metadata-only records.
- Independently downloaded all 24 release assets over public URLs without
  authentication; every SHA-256 matches the GitHub asset digest.
- Individual JSON/schema/source downloads match their archived counterparts.
- `scripts/releases.py::verify_directory` passed against the downloaded package:
  semantic dataset identity, file inventory/hashes, archive checksum and contents.
- All 16 JSON documents pass their published JSON Schemas.
- `target/release/hots-reference validate <downloaded-package>` passed and
  reported the published dataset version.

The first failed run's release remains an unpublished staging draft. It was not
promoted and is not an additional public data release.

## Real unchanged-publication check

[Run 43310915872](https://github.com/matella/HeroesOfTheStormPatchNotes/actions/runs/43310915872)
ran the same workflow and commit on `main` with `publish=true`. Both build and
publication succeeded. The publisher reported:

```text
unchanged bf6c543f34733ad61811f6b4dc635908892e2949b9284f683e577cd3d4c9c0c9
```

Compared authenticated release-list and latest-release API snapshots before and
after this run: no new release IDs, including drafts; latest ID/tag/publication
time and all 24 asset IDs/digests stayed identical. There is one published release.
Public `releases/latest/download/manifest.json` and `frfr.json` also match the
independently verified immutable release downloads.

The actual first-publication and no-change paths are verified. Replacing a
previous dataset after a future upstream content change and interrupted-upload
recovery remain covered by the 12 automated publication tests; no artificial
changed dataset was published for testing. Future scheduled execution has not
yet occurred; the workflow is active and its cron uses this same implementation.

## Recovery

Retain immutable releases. If necessary, promote a previous fully validated release
as latest; never remove the working latest assets before a verified replacement.
No deployed API or consumer migration is part of this verification.
