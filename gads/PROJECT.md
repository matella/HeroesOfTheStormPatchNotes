# Project rules

## Purpose and scope

A Rust CLI producing a canonical Heroes of the Storm reference dataset and static
JSON releases. Issue #1 authorizes replacing the .NET API, database and Blazor app,
removing obsolete sources/configuration, and breaking Storm Codex compatibility.
The existing consumer will adapt later. Git history preserves the old application.
Do not modify deployed services or publish production assets as a side effect of
local development.

## Layout and commands

- `src/model.rs`: canonical typed records and JSON Schema definitions.
- `src/upstream.rs`: pinned v5 full/JSON Patch reconstruction.
- `src/normalize.rs`: game structures, localized text, map overrides and asset references.
- `src/patches.rs`: official discovery, historical metadata and section relationships.
- `src/http.rs`: bounded acquisition, allowlisted HTTPS origins, immutable cache.
- `src/export.rs`: integrity validation and deterministic packaging.
- `data/`: historical source registry, license inventory and attributions.
- `scripts/releases.py`: stage/verify/promote immutable GitHub releases.
- `.github/workflows/`: quality checks and nightly/manual generation.
- `docs/adr/`, `docs/tasks/`: architectural decisions and verification evidence.

Commands: `cargo fmt --check`; `cargo clippy --locked --all-targets -- -D warnings`;
`cargo test --locked`; `python3 -m unittest discover -s scripts -p 'test_*.py'`;
`cargo run --release -- build`; `cargo run --release -- validate dist`.

## Data and identity rules

- Data quality precedes consumer adapters. No legacy database-generated primary IDs.
- Hero IDs are native upstream hero keys; `attributeId` and `hyperlinkId` are aliases.
- Map IDs use `normalizedId`: native `mapId` can be blank or shared by variants.
- Ability/talent relationships retain upstream `linkId` and talent tiers.
- Keep all 12 upstream game-data locales. Missing translations must be reported;
  no silent English substitution or invented official translations.
- Keep live history and only the newest active/candidate PTR cycle. Reconstruct
  obsolete PTR dependencies internally where required, without exporting PTR history.
- Preserve source authority and identity. Do not merge different builds by date or
  article URL alone. Missing historical note content may remain metadata-only;
  this exception is explicitly approved by the owner.
- Keep native structural fields and map-specific overrides; do not silently drop
  sub-abilities, hero units, summoned units or scaling data.

## Source and redistribution boundaries

Primary sources: HeroesToolChest/heroes-data2 and heroes-images (repository MIT
licenses, Blizzard game/asset attribution retained); heroespatchnotes/heroes-patch-data
(MIT historical metadata); existing BlueTracker/Blizzard official-note path.
Fixed archives of existing official posts may repair broken source URLs.

Nexus Patch Notes is All Rights Reserved (Elitesparkle). Written redistribution
permission is still outstanding. Do not copy its notes/images into runtime inputs,
fixtures or released data. Prior read-only inspection only discovered underlying
source links. The baseline suite archive contains Nexus records and is NOT a
blanket content-reuse permission. New external sources need confirmed reuse terms.
The source registry defaults uncertain community content to metadata/link-only;
enabling `allowContent` requires a documented source/attribution decision.

## Engineering and release rules

Use focused modules, checked errors, BTreeMap for stable canonical ordering, and
fixture tests for upstream formats and failure cases. Pin Cargo dependencies with
Cargo.lock. Treat scraped text and URLs as untrusted. Do not log tokens or embed
machine-specific paths in artifacts. Never disable TLS verification.

Validate a staging dataset before replacing output. Use fixed archive metadata,
content digests and no run timestamps. No-op runs do not publish. Create immutable
release assets, verify their hashes, then promote latest. Never delete the working
latest asset first. GitHub release publication/merge/deployed-service changes need
the applicable release gate; preparing code and workflow definitions is allowed.

Do not edit GADS-managed files (AGENTS.md, CLAUDE.md, gads/standard/, gads/agents/,
gads/templates/, gads/hooks/, .claude/agents/gads-*). Use independent Skeptic and
Reviewer roles as required by CLAUDE.md, and retain their role labels in reports.
