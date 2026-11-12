//! Synthetic fixtures only: no copied Nexus or third-party article content.
use crate::model::*;
use serde_json::{Value, json};
use std::{
    collections::BTreeMap,
    path::PathBuf,
    sync::atomic::{AtomicU64, Ordering},
};

pub fn directory(label: &str) -> PathBuf {
    static NEXT: AtomicU64 = AtomicU64::new(0);
    let p = std::env::temp_dir().join(format!(
        "hots-{label}-{}-{}",
        std::process::id(),
        NEXT.fetch_add(1, Ordering::Relaxed)
    ));
    std::fs::create_dir_all(&p).unwrap();
    p
}
pub fn game() -> GameData {
    let mut tiers = serde_json::Map::new();
    for level in [1, 4, 7, 10, 13, 16, 20] {
        tiers.insert(format!("Level{level}"),json!([{"linkId":format!("T|B|Q|Level{level}"),"talentId":format!("T{level}"),"icon":"talent.png"}]));
    }
    GameData {
        version: "2.0.0.10".into(),
        document_meta: ["herodata", "mapdata", "unitdata", "veterancydata"]
            .into_iter()
            .map(|k| (k.into(), json!({"heroesVersion":"2.0.0.10"})))
            .collect(),
        heroes: BTreeMap::from([(
            "Hero".into(),
            Hero {
                id: "Hero".into(),
                attribute_id: "Hero".into(),
                hyperlink_id: "Hero".into(),
                unit_id: "HeroUnit".into(),
                structure: json!({"talents":tiers,"portraits":{"loading":"hero.png"}}),
            },
        )]),
        battlegrounds: BTreeMap::from([(
            "map".into(),
            Battleground {
                id: "map".into(),
                map_id: None,
                source_key: "Map".into(),
                structure: json!({"loadingScreenImage":"map.png"}),
            },
        )]),
        units: json!({}),
        veterancy: json!({}),
        map_overrides: BTreeMap::new(),
    }
}
fn fields(name: &str) -> Fields {
    BTreeMap::from([(
        "name".into(),
        TextValue::Text {
            text: name.into(),
            html: name.into(),
        },
    )])
}
pub fn locale(code: &str) -> Localization {
    let mut talents = BTreeMap::new();
    for level in [1, 4, 7, 10, 13, 16, 20] {
        let mut f = fields("Talent");
        f.insert(
            "fullText".into(),
            TextValue::Text {
                text: "Description".into(),
                html: "Description".into(),
            },
        );
        talents.insert(format!("T|B|Q|Level{level}"), f);
    }
    Localization {
        locale: code.into(),
        version: "2.0.0.10".into(),
        heroes: BTreeMap::from([("Hero".into(), fields("Hero"))]),
        abilities: BTreeMap::new(),
        talents,
        battlegrounds: BTreeMap::from([("map".into(), fields("Map"))]),
        units: BTreeMap::new(),
        map_overrides: BTreeMap::new(),
        missing: Vec::new(),
    }
}
pub fn patch() -> Patch {
    Patch {
        id: "live:10".into(),
        aliases: vec!["original".into()],
        name: "Synthetic release".into(),
        channel: Channel::Live,
        patch_type: PatchType::Live,
        ptr_state: None,
        cycle_version: None,
        date: "2020-01-01".into(),
        build: Some("10".into()),
        sources: Vec::new(),
        availability: Availability::MetadataOnly,
        reason: Some("Synthetic metadata".into()),
        contents: BTreeMap::new(),
    }
}
pub fn write_cache(path: &std::path::Path, url: &str, value: Value) {
    std::fs::write(
        path.join(crate::http::digest(url.as_bytes())),
        serde_json::to_vec(&value).unwrap(),
    )
    .unwrap();
}
