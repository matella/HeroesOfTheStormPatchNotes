mod export;
#[cfg(test)]
mod fixtures;
mod http;
mod model;
mod normalize;
mod patches;
mod upstream;

use anyhow::{Context, Result, ensure};
use clap::{Parser, Subcommand};
use model::*;
use std::{collections::BTreeMap, fs, path::PathBuf};

#[derive(Parser)]
#[command(
    version,
    about = "Canonical multilingual Heroes of the Storm reference data"
)]
struct Cli {
    #[command(subcommand)]
    command: Command,
}
#[derive(Subcommand)]
enum Command {
    /// Acquire, normalize, validate and package all twelve locales and shared assets.
    Build {
        #[arg(long, default_value = "dist")]
        output: PathBuf,
        #[arg(long, default_value = ".cache")]
        cache: PathBuf,
        #[arg(long, default_value = "data/history-sources.json")]
        history_sources: PathBuf,
        /// Previously published patches.json, used to preserve established IDs and PTR cycles.
        #[arg(long)]
        previous: Option<PathBuf>,
    },
    /// Validate an already generated dataset without network access.
    Validate {
        #[arg(default_value = "dist")]
        directory: PathBuf,
    },
    /// Write JSON Schemas without acquiring external sources.
    Schemas {
        #[arg(default_value = "schemas")]
        directory: PathBuf,
    },
}
fn main() -> Result<()> {
    match Cli::parse().command {
        Command::Build {
            output,
            cache,
            history_sources,
            previous,
        } => build(output, cache, history_sources, previous),
        Command::Validate { directory } => {
            let m = export::validate(&directory)?;
            println!("valid {}", m.dataset_version);
            Ok(())
        }
        Command::Schemas { directory } => {
            let temp = directory.join(".schema-stage");
            fs::create_dir_all(&temp)?;
            export::schemas(&temp)?;
            for file in fs::read_dir(temp.join("schemas"))? {
                let file = file?;
                fs::rename(file.path(), directory.join(file.file_name()))?;
            }
            fs::remove_dir_all(temp)?;
            Ok(())
        }
    }
}
fn build(
    output: PathBuf,
    cache: PathBuf,
    history_sources: PathBuf,
    previous_file: Option<PathBuf>,
) -> Result<()> {
    ensure!(
        output.file_name().is_some(),
        "output must be a named dataset directory"
    );
    ensure!(!output.is_symlink(), "output cannot be a symlink");
    let http = http::Http::new(&cache)?;
    let data_sha = http.pin(upstream::DATA_REPO)?;
    let image_sha = http.pin("HeroesToolChest/heroes-images")?;
    let index_sha = http.pin("heroespatchnotes/heroes-patch-data")?;
    let mut up = upstream::Upstream::new(http.clone(), data_sha.clone());
    let (live, ptr) = up.versions()?;
    eprintln!(
        "live={live}; active PTR candidate={}",
        ptr.active_version.as_deref().unwrap_or("none")
    );
    let history: serde_json::Value = serde_json::from_slice(
        &fs::read(history_sources).context("read historical source registry")?,
    )?;
    let previous: Vec<Patch> = if output.join("patches.json").exists() {
        serde_json::from_slice(&fs::read(output.join("patches.json"))?)?
    } else if let Some(path) = previous_file {
        serde_json::from_slice(&fs::read(path)?)?
    } else {
        Vec::new()
    };
    let game = normalize::game(&mut up, &live)?;
    let mut locales = BTreeMap::new();
    for locale in LOCALES {
        eprintln!("normalize {locale}");
        locales.insert(
            locale.into(),
            normalize::localization(&mut up, &game, locale)?,
        );
    }
    eprintln!("acquire patch history and current official notes");
    let patches = patches::acquire(
        &http,
        &index_sha,
        &game,
        &locales["enus"],
        &history,
        &previous,
        &ptr,
    )?;
    // All acquisition occurs before touching the last successful output directory.
    if output.exists() {
        ensure!(
            output.join("manifest.json").exists(),
            "refusing to replace a directory that is not a dataset"
        );
        export::validate(&output)?;
    }
    let parent = output
        .parent()
        .filter(|p| !p.as_os_str().is_empty())
        .unwrap_or(std::path::Path::new("."));
    fs::create_dir_all(parent)?;
    let stage = parent.join(format!(".hots-stage-{}", std::process::id()));
    ensure!(
        !stage.exists(),
        "staging directory already exists: {}",
        stage.display()
    );
    fs::create_dir(&stage)?;
    let result = (|| -> Result<Manifest> {
        export::write_json(&stage, "game.json", &game)?;
        for (locale, v) in &locales {
            export::write_json(&stage, &format!("locales/{locale}.json"), v)?;
        }
        export::write_json(&stage, "patches.json", &patches)?;
        let paths = normalize::asset_paths(&game)?;
        eprintln!("acquire {} shared assets", paths.len());
        let assets = export::assets(&http, &image_sha, &paths, &stage)?;
        export::write_json(&stage, "assets.json", &assets)?;
        export::schemas(&stage)?;
        // Include the checked source/license inventory in every published dataset.
        fs::copy("data/SOURCES.md", stage.join("SOURCES.md"))?;
        fs::create_dir_all(stage.join("licenses"))?;
        for file in fs::read_dir("data/licenses")? {
            let f = file?;
            fs::copy(f.path(), stage.join("licenses").join(f.file_name()))?;
        }
        let mut sources = http.inputs.lock().expect("input lock").clone();
        sources.insert("HeroesToolChest/heroes-data2".into(), data_sha);
        sources.insert("HeroesToolChest/heroes-images".into(), image_sha);
        sources.insert("heroespatchnotes/heroes-patch-data".into(), index_sha);
        export::finish(&stage, &live, &locales, &game, &patches, sources)
    })();
    let manifest = match result {
        Ok(m) => m,
        Err(e) => {
            fs::remove_dir_all(&stage)?;
            return Err(e);
        }
    };
    if output.exists() {
        let old = export::validate(&output)?;
        if old.dataset_version == manifest.dataset_version {
            fs::remove_dir_all(stage)?;
            println!("unchanged {}", manifest.dataset_version);
            return Ok(());
        }
    }
    let backup = parent.join(format!(".hots-backup-{}", std::process::id()));
    ensure!(!backup.exists(), "backup path exists");
    if output.exists() {
        fs::rename(&output, &backup)?;
    }
    if let Err(e) = fs::rename(&stage, &output) {
        if backup.exists() {
            fs::rename(&backup, &output)?;
        }
        return Err(e.into());
    }
    if backup.exists() {
        fs::remove_dir_all(backup)?;
    }
    println!("built {} at {}", manifest.dataset_version, output.display());
    Ok(())
}
