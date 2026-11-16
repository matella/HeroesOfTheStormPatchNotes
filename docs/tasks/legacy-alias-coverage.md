# Legacy patch IDs, mirror duplicates, hero icons and map slugs — 2026-11-16

## Scope

Storm Codex moved from the retired HotsPatchNotes API to this dataset and is on hold
because release `data-c8d8ff8f…` covered only 97 of the 300 patch IDs the old API
served (276 patches against 300). Old `/patch/<id>` links would break. Requested:

1. cover the 203 missing old IDs (aliases, or include the patches that are dropped);
2. bundle the square hero icons (`portraits.heroSelect`) and list them in `assets.json`;
3. give every battleground a file-name-safe image name ("Checkpoint: Hanamura" has a `:`).

Not in scope: publishing a release, changing the Storm Codex consumer.

## Changes

- `patches.rs` — `article_key`: a BlueTracker forum topic with a 7+ digit number
  mirrors the Blizzard news article with that number. Reconciliation now uses it, so
  the 16 articles of 2021–2024 that appeared twice (forum mirror + blog mirror, or
  forum mirror + heroes-patch-data build record) become one patch each.
- `patches.rs` — `apply_legacy_aliases` + `data/legacy-aliases.json` (new
  `--legacy-aliases` build option): old ID → dataset patch, by ID or existing alias.
  A missing or ambiguous target, or an old ID that collides with an existing identity,
  fails the build; `ifPresent` entries (the current PTR) may be absent.
- `normalize.rs`/`export.rs` — `heroSelect` icons are required assets; battlegrounds
  carry a `slug`; validation checks slug uniqueness/format and that every asset name
  uses only `[A-Za-z0-9-_.()]`.

`Battleground.slug` is a new required field in `game.json`. It is additive, so the
schema version stays 1; a consumer that rejects unknown fields would need updating.
The slug equals Storm Codex's current `map_slug` for every map except
`checkpoint-hanamura` (previously `checkpoint:-hanamura`), so the consumer should
read `slug` instead of deriving the name (and its front-end must drop `:` the same way).

## Coverage of the 203 old IDs

| Result | Count | Detail |
| --- | ---: | --- |
| Aliased | 157 | 119 "unique", 28 "ambiguous", 10 "absent" (dated 1–14 days off) |
| Still missing: old PTR notes | 19 | Dropped by the project rule that exports only the newest PTR cycle |
| Still missing: no official source | 25 | Hotfixes/patches known only from Nexus Patch Notes |
| Still missing: Nexus-only same-day hotfix | 2 | `nexus-2025-12-12-revert`, `nexus-2026-04-20-hotfix` |

One alias, `ptr-patch-notes-november-9-2026`, targets the current PTR and is marked
`ifPresent`; it disappears when the next PTR cycle replaces it, like the other PTRs.

Matching rules: same date and compatible type ("unique"); for 2018-06-12 by type
(patch vs. hotfix); the other "ambiguous" pairs were one article seen twice, now
merged; "absent" slug IDs whose old date was the EU repost date map to the article
named by their title date; `nexus-2018-07-25-live` maps to `live:66810`, whose
heroes-patch-data ID is `balance-update-notes-july-25-2018`.

Not aliased on purpose: an old Nexus "hotfix" or "revert" on the same day as a
different official patch is a separate event, not that patch.

## Open decisions

- Export historical PTR notes (19 IDs)? It needs a change to the project rule
  "only the newest PTR cycle".
- The 25 Nexus-only IDs need an official source (Blizzard forum posts are not
  discovered today) or stay missing; Nexus content cannot be ingested.
