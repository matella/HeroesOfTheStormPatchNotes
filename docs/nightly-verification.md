# Hosted nightly verification — 2026-11-12

The owner explicitly requested running the nightly workflow and publishing the
required releases. PR #2 was merged as
`993a594e122c1bc558f0cb8a2f94b355e54f2067` after its quality checks passed.
The post-merge Rust quality run also passed.

## Observable plan

- [ ] Run the complete workflow on `main` with `publish=true` and verify promotion.
- [ ] Independently download public assets, verify hashes and archive contents,
  validate JSON Schemas, and run the Rust dataset validator.
- [ ] Run the same workflow again with `publish=true`; verify an unchanged result
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
release. Hosted verification of the correction is still pending below.

## Recovery

Retain immutable releases. If necessary, promote a previous fully validated release
as latest; never remove the working latest assets before a verified replacement.
No deployed API or consumer migration is part of this verification.
