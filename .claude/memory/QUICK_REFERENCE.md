# Quick reference

Run commands from the repository root. rustup reads the pinned toolchain and
installs the declared rustfmt/clippy components. The Python publisher needs Python
3 and no third-party packages. GitHub-hosted publication also uses the GitHub CLI.

## Local quality gates

```sh
cargo fmt --check
cargo clippy --locked --all-targets -- -D warnings
cargo test --locked
python3 -m unittest discover -s scripts -p 'test_*.py'
```

## Generate, inspect and validate

```sh
cargo run --locked --release -- --help
cargo run --locked --release -- build
cargo run --locked --release -- validate dist
cargo run --locked -- schemas schemas
```

`build` supports `--output`, `--cache`, `--history-sources` and `--previous`.
Defaults are `dist`, `.cache` and `data/history-sources.json`. `--previous` accepts
previously published `patches.json`; an existing validated output supplies its own
prior patch state. `GITHUB_TOKEN` is optional for authenticated acquisition.

## Hosted state and manual generation

```sh
gh workflow view nightly.yml
gh run list --workflow nightly.yml
gh workflow run nightly.yml --ref main -f publish=false
gh release view --repo matella/HeroesOfTheStormPatchNotes
```

A manual dispatch defaults to artifact-only generation. Publishing with
`publish=true`, merging, or changing deployed services requires authorization under
AGENTS.md/GADS. The implementation itself does not supply that approval.

The release script reads `GH_TOKEN` and `GITHUB_REPOSITORY`. Its `previous` command
writes the last published patch array (or an empty array before the first release).
Its `publish` command validates the package, stages uploads and promotes latest
only when content changes. Prefer the reviewed workflow for hosted publication.

Use [README](../../README.md) for public download URLs and locale/schema layout.
No database startup, HTTP sync endpoint or separate frontend build is required.
