use crate::{model::*, upstream::Upstream};
use anyhow::{Context, Result, ensure};
use scraper::Html;
use serde_json::Value;
use std::collections::{BTreeMap, BTreeSet};

pub fn clean_html(raw: &str) -> String {
    let replaced = raw.replace("<n/>", "<br>").replace("<n />", "<br>");
    ammonia::Builder::default()
        .url_relative(ammonia::UrlRelative::Deny)
        .clean(&replaced)
        .to_string()
}
pub fn plain(raw: &str) -> String {
    let safe = clean_html(raw);
    Html::parse_fragment(&safe)
        .root_element()
        .text()
        .collect::<Vec<_>>()
        .join(" ")
        .split_whitespace()
        .collect::<Vec<_>>()
        .join(" ")
}
fn gamestring_html(raw: &str) -> String {
    static STYLE: std::sync::OnceLock<regex::Regex> = std::sync::OnceLock::new();
    let style =
        STYLE.get_or_init(|| regex::Regex::new(r"(?i)<s(?:\s[^>]*)?>").expect("style regex"));
    clean_html(&style.replace_all(raw, "<span>").replace("</s>", "</span>"))
}
fn text_value(value: &Value) -> Result<TextValue> {
    match value {
        Value::String(raw) => Ok(TextValue::Text {
            text: plain(&gamestring_html(raw)),
            html: gamestring_html(raw),
        }),
        Value::Array(v) => Ok(TextValue::List(
            v.iter().map(text_value).collect::<Result<_>>()?,
        )),
        _ => anyhow::bail!("unexpected gamestring value type"),
    }
}
fn section(items: &Value, key: &str) -> Result<BTreeMap<String, Fields>> {
    let mut result: BTreeMap<String, Fields> = BTreeMap::new();
    let fields = items[key]
        .as_object()
        .with_context(|| format!("missing gamestring section: {key}"))?;
    for (field, entries) in fields {
        for (id, value) in entries
            .as_object()
            .context("gamestring field is not a dictionary")?
        {
            result
                .entry(id.clone())
                .or_default()
                .insert(field.clone(), text_value(value)?);
        }
    }
    Ok(result)
}
fn required<'a>(v: &'a Value, key: &str) -> Result<&'a str> {
    v[key]
        .as_str()
        .filter(|s| !s.is_empty())
        .with_context(|| format!("missing {key}"))
}

pub fn game(up: &mut Upstream, version: &str) -> Result<GameData> {
    let native = up.document(version, "[data]", "herodata")?;
    let maps = up.document(version, "[data]", "mapdata")?;
    let units = up.document(version, "[data]", "unitdata")?;
    let veterancy = up.document(version, "[data]", "veterancydata")?;
    let mut heroes = BTreeMap::new();
    let mut attributes = BTreeSet::new();
    for (id, v) in native["items"].as_object().context("missing hero items")? {
        let attribute = required(v, "attributeId")?;
        ensure!(
            attributes.insert(attribute.to_owned()),
            "duplicate attribute ID {attribute}"
        );
        heroes.insert(
            id.clone(),
            Hero {
                id: id.clone(),
                attribute_id: attribute.into(),
                hyperlink_id: required(v, "hyperlinkId")?.into(),
                unit_id: required(v, "unitId")?.into(),
                structure: v.clone(),
            },
        );
    }
    let mut battlegrounds = BTreeMap::new();
    for (source, v) in maps["items"].as_object().context("missing map items")? {
        let id = required(v, "normalizedId")?;
        ensure!(!battlegrounds.contains_key(id), "duplicate map ID {id}");
        battlegrounds.insert(
            id.into(),
            Battleground {
                id: id.into(),
                map_id: v["mapId"]
                    .as_str()
                    .filter(|s| !s.is_empty())
                    .map(str::to_owned),
                source_key: source.clone(),
                structure: v.clone(),
            },
        );
    }
    ensure!(
        !heroes.is_empty() && !battlegrounds.is_empty(),
        "empty game inventory"
    );
    Ok(GameData {
        version: version.into(),
        document_meta: BTreeMap::from([
            ("herodata".into(), native["meta"].clone()),
            ("mapdata".into(), maps["meta"].clone()),
            ("unitdata".into(), units["meta"].clone()),
            ("veterancydata".into(), veterancy["meta"].clone()),
        ]),
        heroes,
        battlegrounds,
        units: units["items"].clone(),
        veterancy: veterancy["items"].clone(),
        map_overrides: up.map_overrides(
            version,
            "[data]",
            &["herodata", "unitdata", "veterancydata"],
        )?,
    })
}

pub fn localization(up: &mut Upstream, game: &GameData, locale: &str) -> Result<Localization> {
    let gs = up.document(&game.version, "[gamestrings]", locale)?;
    ensure!(
        gs["meta"]["gameStringText"]["locale"]
            .as_str()
            .map(str::to_lowercase)
            .as_deref()
            == Some(locale),
        "mixed locale in gamestrings {locale}"
    );
    let maps = up.document(&game.version, "[gamestrings]", &format!("mapdata|{locale}"))?;
    let heroes = section(&gs["items"], "hero")?;
    let abilities = section(&gs["items"], "ability")?;
    let talents = section(&gs["items"], "talent")?;
    let units = section(&gs["items"], "unit")?;
    let map_text = section(&maps["items"], "map")?;
    let mut battlegrounds = BTreeMap::new();
    let mut missing = Vec::new();
    for (id, map) in &game.battlegrounds {
        if let Some(v) = map_text.get(&map.source_key) {
            battlegrounds.insert(id.clone(), v.clone());
        } else {
            missing.push(format!("battleground:{id}"));
        }
    }
    for (id, hero) in &game.heroes {
        if !heroes.get(id).is_some_and(|v| v.contains_key("name")) {
            missing.push(format!("hero:{id}:name"));
        }
        for (tier, values) in hero.structure["talents"]
            .as_object()
            .context("hero talents missing")?
        {
            ensure!(
                [
                    "Level1", "Level4", "Level7", "Level10", "Level13", "Level16", "Level20"
                ]
                .contains(&tier.as_str()),
                "unknown talent tier {tier}"
            );
            let mut ids = BTreeSet::new();
            for talent in values.as_array().context("talent tier must be array")? {
                let key = required(talent, "linkId")?;
                ensure!(ids.insert(key), "duplicate talent link {id}/{tier}/{key}");
                for field in ["name", "fullText"] {
                    if !talents.get(key).is_some_and(|v| v.contains_key(field)) {
                        missing.push(format!("talent:{key}:{field}"));
                    }
                }
            }
        }
    }
    // Per-table RFC 6902 operations preserve removals as well as additions/changes.
    let mut map_overrides = BTreeMap::new();
    for (map, patches) in up.map_overrides(&game.version, "[gamestrings]", &[locale])? {
        let mut overridden = gs.clone();
        crate::upstream::apply(&mut overridden, patches[locale].clone())?;
        let mut delta = serde_json::Map::new();
        for key in ["hero", "ability", "talent", "unit"] {
            // Identify changed IDs in native dictionaries before expensive HTML normalization.
            let before = gs["items"][key]
                .as_object()
                .context("missing override base section")?;
            let after = overridden["items"][key]
                .as_object()
                .context("missing override section")?;
            let mut affected = BTreeSet::new();
            for field in before.keys().chain(after.keys()).collect::<BTreeSet<_>>() {
                let a = before.get(field).and_then(Value::as_object);
                let b = after.get(field).and_then(Value::as_object);
                for id in a
                    .into_iter()
                    .flat_map(|m| m.keys())
                    .chain(b.into_iter().flat_map(|m| m.keys()))
                    .collect::<BTreeSet<_>>()
                {
                    if a.and_then(|m| m.get(id)) != b.and_then(|m| m.get(id)) {
                        affected.insert(id.clone());
                    }
                }
            }
            if affected.is_empty() {
                continue;
            }
            fn subset(items: &Value, key: &str, ids: &BTreeSet<String>) -> Result<Value> {
                let mut table = BTreeMap::<String, Fields>::new();
                for (field, entries) in items[key].as_object().context("section missing")? {
                    for id in ids {
                        if let Some(raw) = entries.get(id) {
                            table
                                .entry(id.clone())
                                .or_default()
                                .insert(field.clone(), text_value(raw)?);
                        }
                    }
                }
                Ok(serde_json::to_value(table)?)
            }
            let base = subset(&gs["items"], key, &affected)?;
            let changed = subset(&overridden["items"], key, &affected)?;
            let operations = json_patch::diff(&base, &changed);
            if !operations.0.is_empty() {
                let table = match key {
                    "hero" => "heroes",
                    "ability" => "abilities",
                    "talent" => "talents",
                    "unit" => "units",
                    _ => unreachable!(),
                };
                delta.insert(table.into(), serde_json::to_value(operations)?);
            }
        }
        if !delta.is_empty() {
            map_overrides.insert(map, Value::Object(delta));
        }
    }
    missing.sort();
    let mut result = Localization {
        locale: locale.into(),
        version: game.version.clone(),
        heroes,
        abilities,
        talents,
        battlegrounds,
        units,
        map_overrides,
        missing,
    };
    result.missing = coverage(game, &result)?;
    Ok(result)
}

pub fn coverage(game: &GameData, locale: &Localization) -> Result<Vec<String>> {
    fn has(fields: Option<&Fields>, key: &str) -> bool {
        matches!(fields.and_then(|f|f.get(key)), Some(TextValue::Text{text,..}) if !text.trim().is_empty())
    }
    let mut missing = Vec::new();
    for (id, h) in &game.heroes {
        if !has(locale.heroes.get(id), "name") {
            missing.push(format!("hero:{id}:name"));
        }
        for values in h.structure["talents"]
            .as_object()
            .context("missing talent tiers")?
            .values()
        {
            for t in values.as_array().context("invalid talent tier")? {
                let key = required(t, "linkId")?;
                for field in ["name", "fullText"] {
                    if !has(locale.talents.get(key), field) {
                        missing.push(format!("talent:{key}:{field}"));
                    }
                }
            }
        }
    }
    for id in game.battlegrounds.keys() {
        if !locale.battlegrounds.contains_key(id) {
            missing.push(format!("battleground:{id}"));
        }
    }
    missing.sort();
    Ok(missing)
}

pub fn asset_paths(game: &GameData) -> Result<BTreeSet<String>> {
    fn icon_refs(v: &Value, key: Option<&str>, out: &mut BTreeSet<String>) -> Result<()> {
        match v {
            Value::Object(o) => {
                for (k, v) in o {
                    icon_refs(v, Some(k), out)?;
                }
            }
            Value::Array(a) => {
                for v in a {
                    icon_refs(v, key, out)?;
                }
            }
            Value::String(s) if key == Some("icon") && s.ends_with(".png") => {
                crate::http::safe_path(s)?;
                ensure!(!s.contains('/'), "unexpected icon path");
                out.insert(format!("abilitytalents/{s}"));
            }
            _ => {}
        }
        Ok(())
    }
    let mut out = BTreeSet::new();
    for h in game.heroes.values() {
        icon_refs(&h.structure, None, &mut out)?;
        let portrait = required(&h.structure["portraits"], "loading")?;
        crate::http::safe_path(portrait)?;
        ensure!(!portrait.contains('/'), "unexpected portrait path");
        out.insert(format!("heroportraits/{portrait}"));
    }
    for m in game.battlegrounds.values() {
        let image = required(&m.structure, "loadingScreenImage")?;
        crate::http::safe_path(image)?;
        ensure!(!image.contains('/'), "unexpected map image path");
        out.insert(format!("loadingscreens/{image}"));
    }
    Ok(out)
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn strips_executable_markup_preserves_unicode() {
        let raw = "<script>alert(1)</script><c val=\"red\">Éclair</c><n/><strong>雷</strong><img src=x onerror=evil()>";
        let safe = clean_html(raw);
        assert!(!safe.contains("onerror"));
        assert!(!safe.contains("<script"));
        assert!(plain(raw).contains("Éclair"));
        assert!(plain(raw).contains('雷'));
    }
    #[test]
    fn locale_identity_is_not_display_name() {
        let raw =
            serde_json::json!({"hero":{"name":{"Amazon":"Cassia"},"title":{"Amazon":"Amazone"}}});
        let data = section(&raw, "hero").unwrap();
        assert!(data.contains_key("Amazon"));
        assert!(!data.contains_key("Cassia"));
    }
}

#[cfg(test)]
mod formatting_tests {
    use super::*;
    #[test]
    fn game_style_tag_is_not_strikethrough() {
        let raw = "<s val=\"bfd4fd\" hlt-name=\"Tooltip\">30 secondes</s>";
        let safe = gamestring_html(raw);
        assert!(!safe.contains("<s>"));
        assert!(safe.contains("30 secondes"));
        assert!(clean_html("<s>old patch value</s>").contains("<s>"));
    }
    #[test]
    fn normalized_overrides_can_delete_translation_fields() {
        let before = serde_json::to_value(
            section(
                &serde_json::json!({"talent":{"name":{"id":"Nom"},"fullText":{"id":"Texte"}}}),
                "talent",
            )
            .unwrap(),
        )
        .unwrap();
        let after = serde_json::to_value(
            section(
                &serde_json::json!({"talent":{"name":{"id":"Nom"}}}),
                "talent",
            )
            .unwrap(),
        )
        .unwrap();
        let patch = json_patch::diff(&before, &after);
        let mut applied = before;
        json_patch::patch(&mut applied, &patch).unwrap();
        assert_eq!(applied, after);
        assert!(applied["id"].get("fullText").is_none());
    }
}
