# ADR 0001: canonical reference data, independent of consumers

## Status
Accepted for implementation under issue #1 and the owner's explicit data-first direction.

## Decision
Produce language-independent typed hero/map records retaining upstream structural
data, with localizations keyed by stable native IDs. Source aliases are provenance,
not primary keys. Patches have independent identities per build/channel/release;
multiple sources may describe one release, while one article may cover multiple
builds. Do not merge records by date alone. Preserve source content availability.

Use maintained HeroesToolChest v2 JSON and its published schemas rather than
embedding a .NET extractor. Reconstruct all pinned JSON Patch dependencies before
selecting live/public PTR output. Historical official notes come from the legacy
version index plus paginated BlueTracker discovery and explicitly curated archived
source URLs. Unrecoverable or unapproved text stays metadata-only.

Generate all 12 locales, share assets, and retain upstream raw structural fields to
avoid discarding multi-form abilities, units and level scaling. English is not a
primary key. A locale's unavailable text is recorded, never silently translated.

Manifest and output hashes identify the release without run timestamps. Build in a
new staging directory and replace local output only after validation. Tar metadata
and gzip timestamps are fixed. Immutable release assets are staged before latest
promotion; no deletion of the working latest archive. An unchanged content digest
must not create another release. Consumer adapters are separate later work.

## Dependencies and tradeoffs
Use serde/serde_json and schemars for typed contracts, json-patch for RFC 6902,
reqwest with rustls for TLS acquisition, scraper/ammonia for parsed and sanitized
HTML, sha2/tar/flate2 for verifiable deterministic packaging, clap for the CLI,
and chrono/url/regex for checked discovery/normalization. These are established
Rust libraries under permissive licenses; Cargo.lock pins the resolved graph.
Review cargo metadata/license inventory before release. This intentionally avoids
CASC extraction, a database and a web server at runtime.

## Validation
Fixture tests cover patch chains, locale separation, identities, content recovery,
archive reproducibility, no-op builds and failed validation. Live smoke tests
exercise upstream format changes. A separate reviewer challenges the final diff.
