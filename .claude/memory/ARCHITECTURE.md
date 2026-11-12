# Architecture: Rust reference data producer

## Components

| File | Responsibility |
| --- | --- |
| `src/main.rs` | `build`, `validate` and `schemas` CLI; staged output replacement |
| `src/model.rs` | Typed canonical records, locale list and generated JSON Schemas |
| `src/http.rs` | HTTPS allowlist, redirect checks, native CA trust, bounded downloads and immutable cache |
| `src/upstream.rs` | Pinned v5 full/JSON Patch reconstruction, dependencies and PTR evidence |
| `src/normalize.rs` | Native structures, all locales, map overrides and required image references |
| `src/patches.rs` | Official discovery, historical registry, stable identities and PTR retention |
| `src/export.rs` | Coverage/integrity validation, shared images and deterministic packaging |
| `src/fixtures.rs` | Synthetic Rust regression fixtures |
| `scripts/releases.py` | Previous published state and verified GitHub release promotion |
| `scripts/test_releases.py` | Publication and draft-recovery regression tests |

Paths in this table are relative to the repository root.

## Acquisition and export

1. Pin HeroesToolChest/heroes-data2, HeroesToolChest/heroes-images and
   heroespatchnotes/heroes-patch-data commits for the run.
2. Reconstruct the latest extracted live game documents and their exact patch
   dependencies, including internal PTR dependencies where required.
3. Normalize language-neutral structures and all 12 locale documents. Preserve
   unit/scaling data, native metadata and map-specific RFC 6902 overrides.
4. Reconcile historical metadata with official Blizzard news/forum discoveries via
   BlueTracker and approved archived official sources. Retain prior published
   identities and explicit metadata-only records when text is unavailable.
5. Acquire referenced shared images, generate schemas, validate coverage and file
   closure, then package stable JSON and a deterministic archive in staging.
6. Replace a validated existing output transactionally; unchanged semantic output
   retains the existing dataset. Failed generation preserves successful output.

The [README](../../README.md) defines the actual output contract and reconstruction
rules. [data/SOURCES.md](../../data/SOURCES.md) defines input attribution and reuse.

## Hosted delivery

`quality.yml` checks PRs and pushes to main. `nightly.yml` runs daily at 03:17 UTC
or by manual dispatch. Build has read-only permissions and retrieves the previously
published patches before generating and validating a complete artifact.

Publication is a separate main-only job, enabled for the schedule or explicit
`publish=true`. It compares semantic dataset identity with latest. Changed data is
staged in an archive-addressed draft, with every upload digest checked before
promotion. Draft discovery uses authenticated paginated release listing when tag
lookup cannot find an unpublished draft; final verification uses the release ID.
Publication runs are serialized and do not cancel one another.

Future scheduled execution and actual future upstream changes are distinct from
observed tests; consult [hosted verification](../../docs/nightly-verification.md).
