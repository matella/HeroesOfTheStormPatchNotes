# Gotchas and verified failure modes

## Draft release visibility

A draft without a Git tag may be absent from `GET releases/tags/{tag}` even after
all uploads succeed. Find it through authenticated paginated release listing and
verify assets through `GET releases/{id}` before promotion. This caused the first
real nightly publication failure; the corrected path and retry behavior have
regression coverage. See [hosted verification](../../docs/nightly-verification.md).

## Dataset identity and publication

Semantic `datasetVersion` is derived from payload hashes. Archive identity also
includes provenance in the manifest. A provenance-only change must not create a
new public data release. Archive-addressed drafts prevent interrupted stages with
different provenance from colliding. Never delete latest assets before replacement.
Preserve published `patches.json` identities in fresh build workspaces.

## Native identities and map overrides

Hero dictionary keys are upstream keys, not translated names. Battleground keys
use `normalizedId`; `mapId` can be blank or shared. Do not collapse different builds
by date or article URL alone. Preserve ability/talent `linkId` and native tiers.

Localization map overrides patch the exported locale tables. Native map overrides
instead patch reconstructed v5 documents with `meta` and `items`. Use retained
`documentMeta` and native structures as described in the README. Preserve removals,
unit/veterancy data and sub-abilities; an overlay merge cannot represent deletions.

## Localization and assets

All 12 game-data locales are mandatory. Missing translations must stay explicit;
never silently replace them with English. Official patch-article discovery currently
acquires US English and does not supply 12 article translations. HDP `<s>` styling
is not HTML strikethrough; sanitize formatting while retaining plain text.

Bundled images cover loading portraits, battleground loading art and hero
ability/talent icons. Additional image names in native structures do not guarantee
bundled cosmetic assets. Resolve included paths through `assets.json`.

## Source trust and caching

Only approved HTTPS origins and redirect destinations are allowed. Use native CA
trust; never disable TLS verification. Cache commit-pinned inputs/fixed captures
immutably, but revalidate mutable Blizzard articles on each run. Required acquisition
failures fail the build rather than publish a partial successful-looking dataset.

Nexus notes/images are excluded. Uncertain community content remains metadata/link
only until reuse is approved. Historical `metadataOnly` records are an accepted
limitation with reasons and provenance, not fabricated patch bodies.

## Verification claims

Counts and hashes in verification documents are dated observations. A manual
workflow run verifies the hosted pipeline but does not prove a future cron event
occurred. The latest PTR is a candidate when activity is inferred, not confirmed.
The dataset contains current game state and historical notes, not every historical
game-state snapshot. Old application details are recoverable from Git history.
