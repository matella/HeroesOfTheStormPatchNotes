# Issue #1 — Rust reference data producer

## Scope and authority

Replace the .NET/Blazor application with a Rust data producer. The user explicitly
approved removing obsolete application code, breaking Storm Codex compatibility,
all 12 upstream game-data locales, and metadata-only historical records when
content cannot be recovered. Keep live history and only the active PTR cycle.
Do not modify deployed services or publish release assets without the final release gate.
GADS-managed files stay unchanged. The baseline checkpoint is commit
015c3776940364d5643af29fbf8cb8fddad73c4d.

## Observable plan

- [x] Canonical types, schemas, source inventory and ADR — review stable IDs and source boundaries.
- [x] Pin/reconstruct upstream versions including dependency PTRs — test full and patch chains.
- [x] Normalize all locales, relationships and shared assets — validate coverage and missing translations.
- [x] Reconcile official history/current notes with explicit availability — test aliases and PTR retention.
- [x] Deterministic JSON/archive and transactional output — prove repeat builds are byte-identical.
- [x] Scheduled/manual CI and staged release publication — verify no-op and failure behavior.
- [x] Remove obsolete application and update project-owned documentation.
- [x] fmt, clippy, tests, live acquisition smoke test, independent GADS review.

## Known evidence

Baseline: matella/storm-codex-suite/releases/tag/referential-1. Archive has 90 heroes,
1,918 talents, 500 patch records but reports 608, 211 empty patch details, and no
manifest. It is a comparison input, not a canonical source or a content license.
Nexus is inspiration/link discovery only; do not redistribute its content.
Source audit data and archive URLs were researched in the issue. Provenance and
availability must be explicit. Historical gaps are permitted, fabricated notes are not.

## Release gate

Prepare and review everything locally first. No live API shutdown, GitHub release
publication, or merge is performed by local implementation/testing. Repository
cleanup is explicitly authorized and reversible through Git history.

## Handoff state

Implementation and local quality/data gates passed; independent GADS review approves
handoff. Production publication and consumer migration remain separate release/consumer
actions. See ../verification.md and ../review-issue-1.md for evidence and limits.
