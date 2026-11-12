# Independent GADS review — issue #1

## Roles and outcome

**[Skeptic]** challenged the architecture before implementation. The design retains
native IDs/structures, all twelve locales, exact pinned patch dependencies including
old PTR inputs, source provenance, content-based identity, and staged publication.

**[Reviewer]** inspected the implementation in multiple passes and approved the
final implementation for handoff/review with no material unresolved defect. This
approval does not verify an actual production GitHub publication.

## Findings resolved

- Revalidate mutable historical articles; cache fixed archive captures separately.
- Preserve deletions in normalized localization overrides using RFC 6902.
- Retain native document metadata so consumers can reconstruct map-patch bases.
- Validate file closure, required schema/data files, asset references, identities,
  tiers, localized coverage and content availability, not merely self-listed hashes.
- Inherit hero/map context through section heading ancestry and retain source aliases.
- Keep the latest PTR explicitly a candidate where evidence is inferred; bound
  cycle inference and retire only with corresponding live release-line evidence.
- Preserve prior IDs/builds through enrichment and discovery of additional mirrors.
- Enforce approved HTTPS origins at every redirect hop; normalize HDP style tags.
- Use complete-archive identity for staged release tags, while semantic payload
  identity controls no-op publication. Verify both identities before promotion.
- Skip unextracted latest versions and correct the audited 2020 legacy date typo.

## Independently executed checks

- `cargo fmt --check`: passed.
- `cargo clippy --locked --all-targets -- -D warnings`: passed.
- `cargo test --quiet`: 22 passed.
- `python3 -m unittest discover -s scripts -p 'test_*.py'`: 6 passed.
- The original malformed-dataset reproduction is rejected.
- GADS-managed-file diff: no changes.

The Reviewer did not execute a second live acquisition or publish anything. The
primary developer owns the live-generation/JSON-Schema/no-op evidence recorded in
verification.md. Synthetic test fixtures contain no copied Nexus material.
