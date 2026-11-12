use crate::http::{Http, safe_path};
use anyhow::{Context, Result, bail, ensure};
use serde_json::Value;
use std::collections::{BTreeMap, BTreeSet};

pub const DATA_REPO: &str = "HeroesToolChest/heroes-data2";
#[derive(Debug, Clone, Default)]
pub struct PtrEvidence {
    pub active_version: Option<String>,
    pub latest_version: Option<String>,
    /// Earliest commit of the latest extracted PTR metadata; bounds newer announcements.
    pub first_extracted_date: Option<String>,
}
pub struct Upstream {
    pub http: Http,
    pub sha: String,
    metadata: BTreeMap<String, Value>,
    documents: BTreeMap<(String, String, String), Value>,
}
impl Upstream {
    pub fn new(http: Http, sha: String) -> Self {
        Self {
            http,
            sha,
            metadata: BTreeMap::new(),
            documents: BTreeMap::new(),
        }
    }
    pub fn raw(&self, path: &str) -> Result<Value> {
        safe_path(path)?;
        self.http.json(
            &format!(
                "https://raw.githubusercontent.com/{DATA_REPO}/{}/heroesdata/{path}",
                self.sha
            ),
            true,
        )
    }
    pub fn meta(&mut self, version: &str) -> Result<Value> {
        safe_path(version)?;
        if let Some(v) = self.metadata.get(version) {
            return Ok(v.clone());
        }
        let v = self.raw(&format!("{version}/.hdp.json"))?;
        ensure!(
            v["extracted"].as_bool() == Some(true),
            "version not extracted: {version}"
        );
        self.metadata.insert(version.into(), v.clone());
        Ok(v)
    }
    pub fn versions(&mut self) -> Result<(String, PtrEvidence)> {
        let mut index = self.raw(".version.json")?;
        let versions = index["versions"]
            .as_array()
            .context("missing version list")?
            .clone();
        let mut extracted = Vec::new();
        for version in versions {
            let name = version.as_str().context("invalid version name")?;
            let meta = self.raw(&format!("{name}/.hdp.json"))?;
            if meta["extracted"] == true {
                self.metadata.insert(name.into(), meta);
                extracted.push(version);
            }
        }
        index["versions"] = Value::Array(extracted);
        let (live, active_version) = select_versions(&index)?;
        let mut ptrs: Vec<_> = index["versions"]
            .as_array()
            .context("versions")?
            .iter()
            .filter_map(Value::as_str)
            .filter(|v| v.ends_with("_ptr"))
            .map(|v| Ok((version_key(v)?, v.to_owned())))
            .collect::<Result<_>>()?;
        ptrs.sort();
        let latest_version = ptrs.last().map(|(_, v)| v.clone());
        let mut first_extracted_date = None;
        if let Some(version) = &latest_version {
            let url = format!(
                "https://api.github.com/repos/{DATA_REPO}/commits?path=heroesdata/{version}/.hdp.json&sha={}&per_page=100",
                self.sha
            );
            let commits = self.http.json(&url, true)?;
            let commits = commits.as_array().context("PTR metadata commit history")?;
            ensure!(
                !commits.is_empty() && commits.len() < 100,
                "PTR creation history missing or truncated"
            );
            first_extracted_date = commits
                .iter()
                .filter_map(|c| c["commit"]["committer"]["date"].as_str())
                .map(|d| d[..10.min(d.len())].to_owned())
                .min();
        }
        Ok((
            live,
            PtrEvidence {
                active_version,
                latest_version,
                first_extracted_date,
            },
        ))
    }
    pub fn document(&mut self, version: &str, group: &str, name: &str) -> Result<Value> {
        self.resolve(version, group, name, &mut BTreeSet::new())
    }
    fn resolve(
        &mut self,
        version: &str,
        group: &str,
        name: &str,
        visited: &mut BTreeSet<String>,
    ) -> Result<Value> {
        let key = (version.into(), group.into(), name.into());
        if let Some(v) = self.documents.get(&key) {
            return Ok(v.clone());
        }
        ensure!(
            visited.insert(version.into()),
            "cyclic patch chain: {version}"
        );
        ensure!(visited.len() <= 200, "excessive patch chain");
        let meta = self.meta(version)?;
        let file = meta["files"][group][name]
            .as_str()
            .with_context(|| format!("missing {group}/{name} in {version}"))?;
        safe_path(file)?;
        let folder = if group == "[data]" {
            "data"
        } else {
            "gamestrings"
        };
        let input = self.raw(&format!("{version}/{folder}/{file}"))?;
        let result = match meta["json"].as_str() {
            Some("full") => {
                ensure!(input.is_object(), "full document must be an object");
                input
            }
            Some("patch") => {
                let prior = meta["depends-on"]
                    .as_str()
                    .context("missing patch dependency")?;
                ensure!(!prior.is_empty(), "empty patch dependency");
                let mut base = self.resolve(prior, group, name, visited)?;
                apply(&mut base, input)?;
                base
            }
            _ => bail!("unknown JSON representation in {version}"),
        };
        ensure!(
            result["items"].is_object(),
            "reconstructed {name} lacks items"
        );
        let declared = result["meta"]["heroesVersion"]
            .as_str()
            .context("missing heroesVersion")?;
        ensure!(
            declared == version,
            "mixed versions: wanted {version}, got {declared}"
        );
        visited.remove(version);
        self.documents.insert(key, result.clone());
        Ok(result)
    }
    pub fn map_overrides(
        &mut self,
        version: &str,
        group: &str,
        names: &[&str],
    ) -> Result<BTreeMap<String, BTreeMap<String, Value>>> {
        let meta = self.meta(version)?;
        let mut result = BTreeMap::new();
        if let Some(maps) = meta["files"][group]["[maps]"].as_object() {
            for (map, kinds) in maps {
                safe_path(map)?;
                let mut values = BTreeMap::new();
                for name in names {
                    if let Some(file) = kinds[*name].as_str() {
                        safe_path(file)?;
                        let folder = if group == "[data]" {
                            "data"
                        } else {
                            "gamestrings"
                        };
                        let patch = self.raw(&format!("{version}/{folder}/maps/{map}/{file}"))?;
                        // Verify applicability to this version's base, including test operations.
                        let mut base = self.document(version, group, name)?;
                        apply(&mut base, patch.clone())?;
                        values.insert((*name).into(), patch);
                    }
                }
                if !values.is_empty() {
                    result.insert(map.clone(), values);
                }
            }
        }
        Ok(result)
    }
}
pub fn apply(base: &mut Value, patch: Value) -> Result<()> {
    let operations: json_patch::Patch =
        serde_json::from_value(patch).context("invalid RFC 6902 patch")?;
    json_patch::patch(base, &operations).context("apply upstream patch")?;
    Ok(())
}
pub fn version_key(version: &str) -> Result<Vec<u32>> {
    let base = version.strip_suffix("_ptr").unwrap_or(version);
    let v: Vec<u32> = base
        .split('.')
        .map(str::parse)
        .collect::<std::result::Result<_, _>>()?;
    ensure!(v.len() == 4, "invalid version: {version}");
    Ok(v)
}
pub fn select_versions(index: &Value) -> Result<(String, Option<String>)> {
    let versions = index["versions"]
        .as_array()
        .context("missing version index")?;
    let mut values = Vec::new();
    for v in versions {
        let name = v.as_str().context("invalid version entry")?;
        values.push((version_key(name)?, name.to_owned()));
    }
    values.sort();
    let live = values
        .iter()
        .rev()
        .find(|(_, v)| !v.ends_with("_ptr"))
        .context("no live version")?;
    let ptr = values
        .iter()
        .rev()
        .find(|(k, v)| v.ends_with("_ptr") && k > &live.0)
        .map(|(_, v)| v.clone());
    Ok((live.1.clone(), ptr))
}

#[cfg(test)]
mod tests {
    use super::*;
    use serde_json::json;
    #[test]
    fn chain_operations_and_missing_paths() {
        let mut v = json!({"items":{"a":1}});
        apply(
            &mut v,
            json!([{"op":"replace","path":"/items/a","value":2}]),
        )
        .unwrap();
        apply(&mut v, json!([{"op":"add","path":"/items/b","value":3}])).unwrap();
        assert_eq!(v["items"]["a"], 2);
        assert!(apply(&mut v, json!([{"op":"replace","path":"/absent","value":0}])).is_err());
    }
    #[test]
    fn live_over_superseded_ptr_and_numeric_order() {
        assert_eq!(
            select_versions(&json!({"versions":["2.0.0.9_ptr","2.0.0.10","2.0.0.11_ptr"]}))
                .unwrap(),
            ("2.0.0.10".into(), Some("2.0.0.11_ptr".into()))
        );
        assert!(
            select_versions(&json!({"versions":["2.0.0.10","2.0.0.10_ptr"]}))
                .unwrap()
                .1
                .is_none()
        );
    }
}

#[cfg(test)]
mod resolution_tests {
    use super::*;
    use crate::fixtures::{directory, write_cache};
    use serde_json::json;
    fn seed(cache: &std::path::Path, version: &str, meta: Value, doc: Value) {
        let base =
            format!("https://raw.githubusercontent.com/{DATA_REPO}/aaa/heroesdata/{version}");
        write_cache(cache, &format!("{base}/.hdp.json"), meta);
        write_cache(cache, &format!("{base}/data/hero.json"), doc);
    }
    #[test]
    fn resolves_a_real_dependency_chain_and_detects_cycle() {
        let cache = directory("chain");
        seed(
            &cache,
            "2.0.0.1",
            json!({"json":"full","extracted":true,"files":{"[data]":{"herodata":"hero.json"}}}),
            json!({"meta":{"heroesVersion":"2.0.0.1"},"items":{"Hero":1}}),
        );
        seed(
            &cache,
            "2.0.0.2_ptr",
            json!({"json":"patch","extracted":true,"depends-on":"2.0.0.1","files":{"[data]":{"herodata":"hero.json"}}}),
            json!([{"op":"replace","path":"/meta/heroesVersion","value":"2.0.0.2_ptr"},{"op":"replace","path":"/items/Hero","value":2}]),
        );
        seed(
            &cache,
            "2.0.0.3",
            json!({"json":"patch","extracted":true,"depends-on":"2.0.0.2_ptr","files":{"[data]":{"herodata":"hero.json"}}}),
            json!([{"op":"replace","path":"/meta/heroesVersion","value":"2.0.0.3"},{"op":"add","path":"/items/New","value":3}]),
        );
        let mut up = Upstream::new(Http::new(&cache).unwrap(), "aaa".into());
        let value = up.document("2.0.0.3", "[data]", "herodata").unwrap();
        assert_eq!(value["items"]["Hero"], 2);
        assert_eq!(value["items"]["New"], 3);
        seed(
            &cache,
            "2.0.0.4",
            json!({"json":"patch","extracted":true,"depends-on":"2.0.0.4","files":{"[data]":{"herodata":"hero.json"}}}),
            json!([]),
        );
        assert!(
            up.document("2.0.0.4", "[data]", "herodata")
                .unwrap_err()
                .to_string()
                .contains("cyclic")
        );
        std::fs::remove_dir_all(cache).unwrap();
    }
    #[test]
    fn refuses_unextracted_input() {
        let cache = directory("unextracted");
        seed(&cache, "2.0.0.1", json!({"extracted":false}), json!({}));
        let mut up = Upstream::new(Http::new(&cache).unwrap(), "aaa".into());
        assert!(up.document("2.0.0.1", "[data]", "herodata").is_err());
        std::fs::remove_dir_all(cache).unwrap();
    }
}

#[cfg(test)]
mod selection_tests {
    use super::*;
    use crate::fixtures::{directory, write_cache};
    use serde_json::json;
    #[test]
    fn skips_newer_unextracted_version_without_network() {
        let cache = directory("selection");
        let root = format!("https://raw.githubusercontent.com/{DATA_REPO}/aaa/heroesdata");
        write_cache(
            &cache,
            &format!("{root}/.version.json"),
            json!({"versions":["2.0.0.10","2.0.0.11"],"date":"2026-01-01"}),
        );
        write_cache(
            &cache,
            &format!("{root}/2.0.0.10/.hdp.json"),
            json!({"extracted":true}),
        );
        write_cache(
            &cache,
            &format!("{root}/2.0.0.11/.hdp.json"),
            json!({"extracted":false}),
        );
        let mut up = Upstream::new(Http::new(&cache).unwrap(), "aaa".into());
        let (live, ptr) = up.versions().unwrap();
        assert_eq!(live, "2.0.0.10");
        assert!(ptr.latest_version.is_none());
        std::fs::remove_dir_all(cache).unwrap();
    }
}
