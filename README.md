# HotS Reference Data

A Rust producer for reusable Heroes of the Storm reference data: heroes, abilities,
talents, battlegrounds, localized game text, images, and patch history. No web server,
.NET runtime, database, or Storm Codex installation is needed.

The dataset has its own versioned contract. **It intentionally breaks the former
HotsPatchNotes API/snapshot contract.** Storm Codex adaptation is separate work.

## Generate and validate

Install Rust using rustup (the pinned toolchain file includes rustfmt and clippy), then:

```sh
cargo run --locked --release -- build
cargo run --locked --release -- validate dist
cargo run --locked -- schemas schemas
```

`build` reconstructs the latest **extracted live** HeroesToolChest v5 dataset and
its dependencies, normalizes all twelve locales, discovers official notes, fetches
shared assets, validates the result, and writes a deterministic bulk archive.
`--output`, `--cache`, `--history-sources` select task-specific directories/files.
`--previous /path/to/patches.json` supplies previously published identities and PTR
cycle evidence when starting in a fresh CI workspace. An existing output also
provides this state and must pass validation before it can be replaced.

Use `GITHUB_TOKEN` for authenticated GitHub metadata requests in CI. It is optional
locally; anonymous API limits may apply. No credential is embedded in the output.
HTTPS uses the operating system trust store, including explicitly configured CA
certificates. Required acquisition/validation failures fail the build and preserve
the previous successful output. Unavailable historical text is an approved exception:
its release metadata survives with a reason and provenance.

## Output contract (schema version 1)

```text
dist/
  game.json                  # neutral hero/map records, unit/scaling data, overrides
  locales/{locale}.json      # localized fields keyed by stable native IDs
  patches.json               # canonical releases, aliases, sources, content and sections
  assets.json                # category/filename → path, SHA-256 and byte count
  images/{category}/*.png     # shared assets, including talent/ability icons
  schemas/*.schema.json      # generated JSON Schemas
  licenses/*.txt
  SOURCES.md
  manifest.json              # schema/dataset/build identity, coverage and file hashes
  dataset.tar.gz
  dataset.tar.gz.sha256
```

- Hero dictionary keys are upstream hero keys, such as `Amazon`. Display names
  (such as Cassia) are translations, not identifiers. `attributeId`, `hyperlinkId`
  and `unitId` remain available.
- Battleground keys are upstream `normalizedId`. Native `mapId` is an optional
  alias because it can be blank or shared by normal/heroic variants.
- Native v5 hero structures retain abilities, sub-abilities, talent `linkId`,
  tiers (`Level1`, `Level4`, `Level7`, `Level10`, `Level13`, `Level16`, `Level20`),
  hero units, summoned-unit and level-scaling references. Unit/veterancy tables
  and per-map native patches allow readers to resolve map-specific stats.
- Localized fields expose sanitized `html` and plain `text`; array-valued fields
  remain arrays. HDP styling tags are formatting, not HTML strikethrough. Native
  gamestring growth notation may be retained; clients should not interpret it as
  executable markup. Map objective titles/descriptions are game-derived.
- A localization's `mapOverrides` contains per-table RFC 6902 patches against its
  hero/ability/talent/unit dictionaries; apply them to a copy for that battleground.
  Override table keys match `heroes`, `abilities`, `talents`, and `units`.
  Native `game.mapOverrides` instead target v5 documents: reconstruct each with
  `meta: game.documentMeta[table]` and its native `items` (hero `structure` values,
  `game.units`, or `game.veterancy`) before applying the patch.
- Use `assets.json` to resolve images. Preserve original category/filename identity;
  do not derive asset names from translated names. Bundled core assets are hero
  loading portraits, battleground loading art, and hero ability/talent icons;
  other image names retained in native metadata are not a promise of bundled files.
  Paths are relative to the extracted dataset root. Hosting an extracted archive gives a static JSON/image API.
- A patch has a stable ID and source aliases, channel, date/build when known,
  sources/authority, availability, and locale-keyed article content/sections.
  Strong build identity and recorded source relationships reconcile mirrors;
  different same-day builds are not collapsed by date alone. Community notes
  must not be presented as official.
- `metadataOnly` means no validated/reusable content was acquired, not an empty
  patch. `reason` and sources explain the limitation. A legacy placeholder anchor
  is not content. Known old index date mistakes are corrected in the registry.
- Only the newest PTR announcement cycle is exported. Where extraction evidence
  provides nearby release-line evidence, `cycleVersion` records the inferred line.
  Announcements are explicitly `candidate`, not a claim of confirmed activity.
  Date inference is bounded to three days of the first extracted PTR snapshot;
  older announcements are not relabeled as a newer cycle. A corresponding live
  release retires an associated cycle; unrelated hotfixes do not.

## Languages and completeness

All upstream game-data locales are generated: German `dede`, English `enus`,
Spanish `eses`, Latin American Spanish `esmx`, French `frfr`, Italian `itit`, Korean
`kokr`, Polish `plpl`, Brazilian Portuguese `ptbr`, Russian `ruru`, Simplified Chinese
`zhcn`, and Traditional Chinese `zhtw`.

Each locale records missing required names/descriptions. The manifest reports this
coverage; missing text is never silently replaced with English. Complete locale
source files are mandatory. **These twelve locales do not imply twelve translations
of every historical patch article.** The current article-discovery adapter acquires
US English official content; further official translations need verified discovery.

The output is a current game-reference dataset plus historical release notes, not
a complete reconstruction of game state at every historical patch. The archived
v4 `heroes-data` repository starts in August 2019; it cannot recreate undocumented
2014–2018 prose. The approved fallback retains known release metadata. Read
[data/SOURCES.md](data/SOURCES.md) and the [historical source registry](data/history-sources.json).
No Nexus notes/images are ingested or redistributed.

## Nightly delivery and downloads

The nightly workflow runs at **03:17 UTC**, plus manual dispatch. GitHub schedules
are best-effort. It checks note content independently of game-data availability;
new notes can accompany the last fully extracted live dataset. Mutable official
articles are revalidated so corrections are detected. Old fixed archive captures
and commit-pinned game inputs use an immutable cache.

After the workflow is merged/enabled and its **first release is approved/published**,
the producer repository's download convention is:

```text
https://github.com/matella/HeroesOfTheStormPatchNotes/releases/latest/download/manifest.json
https://github.com/matella/HeroesOfTheStormPatchNotes/releases/latest/download/dataset.tar.gz
https://github.com/matella/HeroesOfTheStormPatchNotes/releases/latest/download/game.json
https://github.com/matella/HeroesOfTheStormPatchNotes/releases/latest/download/patches.json
https://github.com/matella/HeroesOfTheStormPatchNotes/releases/latest/download/frfr.json
```

These are planned producer URLs, not a claim that a release already exists.
Individual language release assets are named `{locale}.json`; inside the archive
(and when statically hosted), they live under `locales/`. Schemas are also separate
release assets; images are included in the bulk archive. The suite's old
`referential.tar.gz` remains a legacy artifact and is not modified by this workflow.

The build job has read-only repository access. Only the publication job receives
`contents: write`. A manual run defaults to building an artifact without publication;
scheduled publication and explicit manual publication are restricted to `main`.
Publication stages a digest-named draft release, uploads and verifies every asset,
and only then promotes latest. Identical dataset content does not create a release.
A failed stage leaves the previous latest release intact and can be retried without
deleting assets. Keep old releases; rollback promotes a previous validated release.
Never use an ephemeral Actions cache as the sole successful-publication state.

## Development and verification

```sh
cargo fmt --check
cargo clippy --locked --all-targets -- -D warnings
cargo test --locked
python3 -m unittest discover -s scripts -p 'test_*.py'
```

Read [AGENTS.md](AGENTS.md), [CLAUDE.md](CLAUDE.md), and [project rules](gads/PROJECT.md).
The [ADR](docs/adr/0001-canonical-rust-dataset.md) and
[task record](docs/tasks/issue-1.md) describe the scope, evidence and release gate.
The previous .NET/Blazor application is preserved in Git history at
`015c3776940364d5643af29fbf8cb8fddad73c4d`. Its removal does not shut down a deployed API.
