# Project memory: HotS Reference Data

## Current project

This repository is a Rust CLI producing reusable JSON reference data and immutable
GitHub data releases. The former application is preserved in Git history; its
server, database and web UI are no longer development dependencies. Storm Codex
consumer adaptation is separate work.

Read [AGENTS.md](../../AGENTS.md), [CLAUDE.md](../../CLAUDE.md) and
[gads/PROJECT.md](../../gads/PROJECT.md) first. These project memories summarize the
implementation; they do not replace the project rules or GADS approval boundaries.

## Start here

- [README](../../README.md): setup, CLI, output contract, languages and downloads.
- [Architecture](ARCHITECTURE.md): modules and data flow.
- [Quick reference](QUICK_REFERENCE.md): development and workflow commands.
- [Gotchas](GOTCHAS.md): identities, source boundaries and publication failures.
- [ADR](../../docs/adr/0001-canonical-rust-dataset.md): data-first architecture.
- [Task record](../../docs/tasks/issue-1.md): accepted scope and handoff.
- [Hosted verification](../../docs/nightly-verification.md): actual release/no-op evidence.

## Verified baseline, 2026-11-12

Rust 1.99.1 / edition 2024; Python 3 uses only its standard library for publication.
Quality gates passed with 22 Rust tests and 12 Python publication tests.
A real hosted build published the first dataset; a second publication-enabled run
reported unchanged and created no release or draft. All 24 public release assets
were independently verified. Counts describe that baseline, not permanent targets.
See the verification records for exact runs, versions, hashes and remaining limits.

## Durable rules

Keep all 12 upstream game-data locales, native IDs, source provenance and explicit
missing-content reasons. Export live patch history and only the newest active or
candidate PTR cycle. Official patch-article discovery currently provides US English;
this is distinct from game-data localization. Do not copy Nexus notes or images.
Preserve previous published identities and verify staged assets before promotion.

Keep these memories concise and update them after architectural, operational or
command changes. Record dated evidence in docs rather than treating old test counts
or generated dataset counts as permanent guarantees.
