# Project documentation and agent-context synchronization — 2026-11-12

## Scope

The owner requested checking all project files, including Claude instructions,
against the completed Rust migration. Correct stale project context and developer
configuration while preserving current code/data contracts and shared GADS files.

## Observable plan

- [x] Inspect tracked files, agent imports, local settings and migration references.
- [x] Replace the four legacy Claude memories with concise current Rust context.
- [x] Remove obsolete tracked personal settings and ignore future local settings.
- [x] Link current memory from imported project rules and development documentation.
- [x] Preserve historical review evidence and add the hosted-publication follow-up.
- [x] Validate links, JSON, managed-file integrity and the final diff.
- [ ] Obtain independent review and pass hosted quality checks.

## Audit evidence

**[Reviewer]** independently confirmed that all 35 managed GADS files match their
installation hashes, all five schemas match regeneration from current Rust, all
249 dependency/version/license entries match locked Cargo metadata, and existing
relative Markdown links resolve. Generic CLAUDE.md imports, shared settings, hooks
and role files remain applicable and require no managed-file edits.

The final local audit parses all nine remaining tracked JSON files, resolves all
relative Markdown links, rechecks all 35 managed hashes, verifies the local settings
file is removed and ignored, and confirms shared hooks allow normal Cargo tests
while asking for force-push approval. The primary memory stays below 200 lines;
obsolete application commands and deleted paths are absent from all four memories.
`git diff --check` passes.

These are documentation and developer-context changes. They do not regenerate or
publish a dataset. The hosted nightly/publication evidence remains in
../nightly-verification.md; no additional production run is needed for this cleanup.
