use crate::{
    http::{Http, digest, safe_path},
    model::*,
};
use anyhow::{Context, Result, ensure};
use flate2::{Compression, GzBuilder};
use serde::Serialize;
use serde_json::Value;
use std::{
    collections::{BTreeMap, BTreeSet},
    fs,
    io::Write,
    path::{Path, PathBuf},
};

fn sorted(v: Value) -> Value {
    match v {
        Value::Object(o) => {
            let ordered: BTreeMap<_, _> = o.into_iter().map(|(k, v)| (k, sorted(v))).collect();
            serde_json::to_value(ordered).expect("JSON object")
        }
        Value::Array(a) => Value::Array(a.into_iter().map(sorted).collect()),
        x => x,
    }
}
pub fn json_bytes(v: &impl Serialize) -> Result<Vec<u8>> {
    let mut b = serde_json::to_vec_pretty(&sorted(serde_json::to_value(v)?))?;
    b.push(b'\n');
    Ok(b)
}
pub fn write_json(root: &Path, name: &str, v: &impl Serialize) -> Result<()> {
    safe_path(name)?;
    let path = root.join(name);
    fs::create_dir_all(path.parent().context("output parent")?)?;
    fs::write(path, json_bytes(v)?)?;
    Ok(())
}
pub fn files(root: &Path) -> Result<Vec<PathBuf>> {
    fn walk(root: &Path, dir: &Path, out: &mut Vec<PathBuf>) -> Result<()> {
        for entry in fs::read_dir(dir)? {
            let e = entry?;
            let t = e.file_type()?;
            ensure!(!t.is_symlink(), "symlinks are not permitted in a dataset");
            if t.is_dir() {
                walk(root, &e.path(), out)?;
            } else {
                ensure!(t.is_file(), "nonregular dataset entry");
                out.push(e.path().strip_prefix(root)?.into());
            }
        }
        Ok(())
    }
    let mut out = Vec::new();
    walk(root, root, &mut out)?;
    out.sort();
    Ok(out)
}
pub fn schemas(root: &Path) -> Result<()> {
    write_json(
        root,
        "schemas/game.schema.json",
        &schemars::schema_for!(GameData),
    )?;
    write_json(
        root,
        "schemas/localization.schema.json",
        &schemars::schema_for!(Localization),
    )?;
    write_json(
        root,
        "schemas/patches.schema.json",
        &schemars::schema_for!(Vec<Patch>),
    )?;
    write_json(
        root,
        "schemas/assets.schema.json",
        &schemars::schema_for!(BTreeMap<String,Asset>),
    )?;
    write_json(
        root,
        "schemas/manifest.schema.json",
        &schemars::schema_for!(Manifest),
    )?;
    Ok(())
}
pub fn assets(
    http: &Http,
    sha: &str,
    paths: &BTreeSet<String>,
    root: &Path,
) -> Result<BTreeMap<String, Asset>> {
    fs::create_dir_all(root.join("images"))?;
    let jobs: Vec<_> = paths.iter().collect();
    let result = std::sync::Mutex::new(Vec::new());
    std::thread::scope(|scope| {
        for chunk in jobs.chunks((jobs.len() / 8).max(1)) {
            let result = &result;
            scope.spawn(move||for path in chunk {
                let work=(||->Result<Asset>{
                    safe_path(path)?;
                    let bytes=http.get(&format!("https://raw.githubusercontent.com/HeroesToolChest/heroes-images/{sha}/heroesimages/{path}"),true)?;
                    ensure!(bytes.starts_with(b"\x89PNG\r\n\x1a\n"),"invalid PNG: {path}");
                    let dest=root.join("images").join(path);
                    fs::create_dir_all(dest.parent().context("asset parent")?)?;
                    fs::write(dest,&bytes)?;
                    Ok(Asset{path:format!("images/{path}"),sha256:digest(&bytes),bytes:bytes.len() as u64})
                })();
                result.lock().expect("asset lock").push(((*path).clone(),work));
            });
        }
    });
    let mut out = BTreeMap::new();
    for (path, value) in result.into_inner().expect("asset lock") {
        out.insert(path, value?);
    }
    Ok(out)
}
pub fn archive(root: &Path, target: &Path) -> Result<()> {
    let out = fs::File::create(target)?;
    let gzip = GzBuilder::new().mtime(0).write(out, Compression::default());
    let mut builder = tar::Builder::new(gzip);
    for path in files(root)? {
        if ["dataset.tar.gz", "dataset.tar.gz.sha256"].contains(&path.to_string_lossy().as_ref()) {
            continue;
        }
        let bytes = fs::read(root.join(&path))?;
        let mut header = tar::Header::new_gnu();
        header.set_size(bytes.len() as u64);
        header.set_mode(0o644);
        header.set_uid(0);
        header.set_gid(0);
        header.set_mtime(0);
        header.set_cksum();
        builder.append_data(&mut header, &path, bytes.as_slice())?;
    }
    builder.into_inner()?.finish()?.sync_all()?;
    Ok(())
}
pub fn finish(
    root: &Path,
    version: &str,
    locales: &BTreeMap<String, Localization>,
    game: &GameData,
    patches: &[Patch],
    sources: BTreeMap<String, String>,
) -> Result<Manifest> {
    let mut hashes = BTreeMap::new();
    for path in files(root)? {
        hashes.insert(
            path.to_string_lossy().replace('\\', "/"),
            digest(&fs::read(root.join(path))?),
        );
    }
    let dataset_version = digest(&json_bytes(&hashes)?);
    let counts = BTreeMap::from([
        ("heroes".into(), game.heroes.len()),
        ("battlegrounds".into(), game.battlegrounds.len()),
        ("patches".into(), patches.len()),
        (
            "metadataOnlyPatches".into(),
            patches
                .iter()
                .filter(|p| p.availability == Availability::MetadataOnly)
                .count(),
        ),
    ]);
    let manifest = Manifest {
        schema_version: SCHEMA_VERSION,
        dataset_version,
        game_version: version.into(),
        locales: locales.keys().cloned().collect(),
        counts,
        missing_translations: locales
            .iter()
            .map(|(k, v)| (k.clone(), v.missing.len()))
            .collect(),
        sources,
        files: hashes,
    };
    write_json(root, "manifest.json", &manifest)?;
    validate(root)?;
    archive(root, &root.join("dataset.tar.gz"))?;
    let hash = digest(&fs::read(root.join("dataset.tar.gz"))?);
    let mut f = fs::File::create(root.join("dataset.tar.gz.sha256"))?;
    writeln!(f, "{hash}  dataset.tar.gz")?;
    Ok(manifest)
}
pub fn validate(root: &Path) -> Result<Manifest> {
    let m: Manifest = serde_json::from_slice(&fs::read(root.join("manifest.json"))?)?;
    ensure!(
        m.schema_version == SCHEMA_VERSION,
        "unsupported schema version"
    );
    ensure!(
        m.locales == LOCALES.map(str::to_owned),
        "all twelve locales are required"
    );
    ensure!(
        m.dataset_version == digest(&json_bytes(&m.files)?),
        "dataset digest mismatch"
    );
    let actual: BTreeSet<String> = files(root)?
        .into_iter()
        .map(|p| p.to_string_lossy().replace('\\', "/"))
        .filter(|p| {
            !["manifest.json", "dataset.tar.gz", "dataset.tar.gz.sha256"].contains(&p.as_str())
        })
        .collect();
    ensure!(
        actual == m.files.keys().cloned().collect(),
        "manifest file inventory mismatch"
    );
    for path in [
        "game.json",
        "patches.json",
        "assets.json",
        "schemas/game.schema.json",
        "schemas/localization.schema.json",
        "schemas/patches.schema.json",
        "schemas/assets.schema.json",
        "schemas/manifest.schema.json",
        "SOURCES.md",
    ] {
        ensure!(m.files.contains_key(path), "required file absent: {path}");
    }
    for locale in LOCALES {
        ensure!(
            m.files.contains_key(&format!("locales/{locale}.json")),
            "required locale file absent"
        );
    }
    for (path, hash) in &m.files {
        safe_path(path)?;
        ensure!(
            digest(&fs::read(root.join(path)).with_context(|| format!("missing file {path}"))?)
                == *hash,
            "file hash mismatch: {path}"
        );
    }
    let game: GameData = serde_json::from_slice(&fs::read(root.join("game.json"))?)?;
    ensure!(
        game.version == m.game_version && !game.heroes.is_empty(),
        "invalid game inventory"
    );
    ensure!(
        !game.battlegrounds.is_empty(),
        "empty battleground inventory"
    );
    for kind in ["herodata", "mapdata", "unitdata", "veterancydata"] {
        ensure!(
            game.document_meta
                .get(kind)
                .and_then(|m| m["heroesVersion"].as_str())
                == Some(game.version.as_str()),
            "native document metadata version mismatch"
        );
    }
    for (map, overrides) in &game.map_overrides {
        ensure!(game.battlegrounds.contains_key(map), "unknown map override");
        for (kind, operations) in overrides {
            let items = match kind.as_str() {
                "herodata" => serde_json::to_value(
                    game.heroes
                        .iter()
                        .map(|(k, h)| (k.clone(), h.structure.clone()))
                        .collect::<BTreeMap<_, _>>(),
                )?,
                "unitdata" => game.units.clone(),
                "veterancydata" => game.veterancy.clone(),
                _ => anyhow::bail!("unknown native override table"),
            };
            let mut native = serde_json::json!({"meta":game.document_meta[kind],"items":items});
            crate::upstream::apply(&mut native, operations.clone())?;
        }
    }
    let mut attributes = BTreeSet::new();
    for (id, hero) in &game.heroes {
        ensure!(
            *id == hero.id
                && !hero.attribute_id.is_empty()
                && !hero.hyperlink_id.is_empty()
                && !hero.unit_id.is_empty(),
            "invalid hero identity"
        );
        ensure!(
            attributes.insert(&hero.attribute_id),
            "duplicate hero attribute"
        );
        let tiers = hero.structure["talents"]
            .as_object()
            .context("missing talent tiers")?;
        let expected: BTreeSet<_> = [
            "Level1", "Level4", "Level7", "Level10", "Level13", "Level16", "Level20",
        ]
        .into_iter()
        .collect();
        ensure!(
            tiers.keys().map(String::as_str).collect::<BTreeSet<_>>() == expected,
            "incomplete talent tiers: {id}"
        );
        for values in tiers.values() {
            let mut ids = BTreeSet::new();
            for t in values.as_array().context("invalid talent tier")? {
                let link = t["linkId"]
                    .as_str()
                    .filter(|s| !s.is_empty())
                    .context("missing talent link")?;
                ensure!(ids.insert(link), "duplicate talent link");
            }
        }
    }
    for (id, map) in &game.battlegrounds {
        ensure!(
            *id == map.id && !map.source_key.is_empty(),
            "invalid map identity"
        );
    }
    for locale in &m.locales {
        let l: Localization =
            serde_json::from_slice(&fs::read(root.join(format!("locales/{locale}.json")))?)?;
        ensure!(
            l.locale == *locale && l.version == game.version,
            "mixed localization version"
        );
        let missing = crate::normalize::coverage(&game, &l)?;
        ensure!(
            missing == l.missing,
            "incorrect translation coverage: {locale}"
        );
        ensure!(
            m.missing_translations.get(locale) == Some(&missing.len()),
            "manifest translation coverage mismatch"
        );
        ensure!(
            game.heroes.keys().all(|id| l.heroes.contains_key(id)),
            "locale missing heroes: {locale}"
        );
    }
    let patches: Vec<Patch> = serde_json::from_slice(&fs::read(root.join("patches.json"))?)?;
    let mut ids = BTreeSet::new();
    for p in &patches {
        ensure!(ids.insert(&p.id), "duplicate patch ID");
        chrono::NaiveDate::parse_from_str(&p.date, "%Y-%m-%d")
            .context("invalid canonical patch date")?;
        ensure!(
            (p.channel == Channel::Ptr) == (p.patch_type == PatchType::Ptr),
            "patch channel/type mismatch"
        );
        ensure!(
            p.availability != Availability::Available || !p.contents.is_empty(),
            "available patch lacks content"
        );
        for c in p.contents.values() {
            ensure!(!c.text.trim().is_empty(), "empty patch body");
            for s in &c.sections {
                if let Some(id) = &s.hero_id {
                    ensure!(game.heroes.contains_key(id), "unknown section hero");
                }
                if let Some(id) = &s.battleground_id {
                    ensure!(game.battlegrounds.contains_key(id), "unknown section map");
                }
            }
        }
    }
    let assets: BTreeMap<String, Asset> =
        serde_json::from_slice(&fs::read(root.join("assets.json"))?)?;
    ensure!(
        assets.keys().cloned().collect::<BTreeSet<_>>() == crate::normalize::asset_paths(&game)?,
        "required asset references differ from inventory"
    );
    for (key, a) in &assets {
        safe_path(&a.path)?;
        ensure!(a.path == format!("images/{key}"), "asset key/path mismatch");
        let bytes = fs::read(root.join(&a.path))?;
        ensure!(
            bytes.len() as u64 == a.bytes && bytes.starts_with(b"\x89PNG\r\n\x1a\n"),
            "invalid PNG asset"
        );
        ensure!(
            m.files.get(&a.path) == Some(&a.sha256),
            "asset hash absent from manifest"
        );
    }
    ensure!(
        patches.iter().filter(|p| p.channel == Channel::Ptr).count() <= 1,
        "duplicate or obsolete PTR announcements exported"
    );
    Ok(m)
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn canonical_json_object_order() {
        assert_eq!(
            json_bytes(&serde_json::json!({"z":1,"a":{"z":2,"a":3}})).unwrap(),
            json_bytes(&serde_json::json!({"a":{"a":3,"z":2},"z":1})).unwrap()
        );
    }
    #[test]
    fn rejects_traversal() {
        for s in ["../bad", "/bad", "a/../b", "a\\b", ""] {
            assert!(safe_path(s).is_err());
        }
        assert!(safe_path("images/a.png").is_ok());
    }
    #[test]
    fn archive_is_reproducible_despite_file_mtimes() {
        let root = std::env::temp_dir().join(format!("hots-archive-{}", std::process::id()));
        let _ = fs::remove_dir_all(&root);
        fs::create_dir_all(&root).unwrap();
        fs::write(root.join("a.json"), b"{}").unwrap();
        let first = root.with_extension("first.tar.gz");
        let second = root.with_extension("second.tar.gz");
        archive(&root, &first).unwrap();
        fs::write(root.join("a.json"), b"{}").unwrap();
        archive(&root, &second).unwrap();
        assert_eq!(fs::read(&first).unwrap(), fs::read(&second).unwrap());
        fs::remove_dir_all(root).unwrap();
        fs::remove_file(first).unwrap();
        fs::remove_file(second).unwrap();
    }
}

#[cfg(test)]
mod dataset_tests {
    use super::*;
    use crate::fixtures;
    fn fixture(root: &Path) -> Manifest {
        let game = fixtures::game();
        let locales: BTreeMap<_, _> = LOCALES
            .into_iter()
            .map(|l| (l.to_string(), fixtures::locale(l)))
            .collect();
        write_json(root, "game.json", &game).unwrap();
        write_json(root, "patches.json", &vec![fixtures::patch()]).unwrap();
        for (code, l) in &locales {
            write_json(root, &format!("locales/{code}.json"), l).unwrap();
        }
        let mut assets = BTreeMap::new();
        for path in crate::normalize::asset_paths(&game).unwrap() {
            let bytes = b"\x89PNG\r\n\x1a\nsynthetic-image-fixture";
            let full = format!("images/{path}");
            fs::create_dir_all(root.join(&full).parent().unwrap()).unwrap();
            fs::write(root.join(&full), bytes).unwrap();
            assets.insert(
                path,
                Asset {
                    path: full,
                    sha256: digest(bytes),
                    bytes: bytes.len() as u64,
                },
            );
        }
        write_json(root, "assets.json", &assets).unwrap();
        schemas(root).unwrap();
        fs::write(root.join("SOURCES.md"), "Synthetic sources").unwrap();
        finish(
            root,
            &game.version,
            &locales,
            &game,
            &[fixtures::patch()],
            BTreeMap::new(),
        )
        .unwrap()
    }
    #[test]
    fn full_fixture_is_repeatable_and_tamper_is_rejected() {
        let a = fixtures::directory("dataset-a");
        let b = fixtures::directory("dataset-b");
        let x = fixture(&a);
        let y = fixture(&b);
        assert_eq!(x.dataset_version, y.dataset_version);
        assert_eq!(
            fs::read(a.join("dataset.tar.gz")).unwrap(),
            fs::read(b.join("dataset.tar.gz")).unwrap()
        );
        fs::write(a.join("game.json"), "{}").unwrap();
        assert!(validate(&a).is_err());
        fs::remove_dir_all(a).unwrap();
        fs::remove_dir_all(b).unwrap();
    }
    #[test]
    fn missing_assets_and_incomplete_manifest_are_rejected() {
        let a = fixtures::directory("bad-dataset");
        let mut m = fixture(&a);
        fs::write(a.join("assets.json"), "{}").unwrap();
        m.files.insert("assets.json".into(), digest(b"{}"));
        m.dataset_version = digest(&json_bytes(&m.files).unwrap());
        write_json(&a, "manifest.json", &m).unwrap();
        assert!(validate(&a).unwrap_err().to_string().contains("asset"));
        m.files.clear();
        m.dataset_version = digest(&json_bytes(&m.files).unwrap());
        write_json(&a, "manifest.json", &m).unwrap();
        assert!(validate(&a).is_err());
        fs::remove_dir_all(a).unwrap();
    }
}
