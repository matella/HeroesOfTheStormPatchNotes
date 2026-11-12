use schemars::JsonSchema;
use serde::{Deserialize, Serialize};
use serde_json::Value;
use std::collections::BTreeMap;

pub const SCHEMA_VERSION: u32 = 1;
pub const LOCALES: [&str; 12] = [
    "dede", "enus", "eses", "esmx", "frfr", "itit", "kokr", "plpl", "ptbr", "ruru", "zhcn", "zhtw",
];

#[derive(Debug, Clone, Serialize, Deserialize, JsonSchema)]
#[serde(rename_all = "camelCase")]
pub struct Hero {
    pub id: String,
    pub attribute_id: String,
    pub hyperlink_id: String,
    pub unit_id: String,
    /// Native v5 structure, including subAbilities, heroUnits and scalingLinkIds.
    pub structure: Value,
}

#[derive(Debug, Clone, Serialize, Deserialize, JsonSchema)]
#[serde(rename_all = "camelCase")]
pub struct Battleground {
    pub id: String,
    pub map_id: Option<String>,
    pub source_key: String,
    pub structure: Value,
}

#[derive(Debug, Clone, Serialize, Deserialize, JsonSchema)]
#[serde(rename_all = "camelCase")]
pub struct GameData {
    pub version: String,
    /// Native document metadata required to apply per-map patches losslessly.
    pub document_meta: BTreeMap<String, Value>,
    pub heroes: BTreeMap<String, Hero>,
    pub battlegrounds: BTreeMap<String, Battleground>,
    /// Unit/scaling tables referenced by hero structures.
    pub units: Value,
    pub veterancy: Value,
    /// RFC 6902 patches against the base tables for each battleground.
    pub map_overrides: BTreeMap<String, BTreeMap<String, Value>>,
}

#[derive(Debug, Clone, Serialize, Deserialize, JsonSchema)]
#[serde(untagged)]
pub enum TextValue {
    Text { text: String, html: String },
    List(Vec<TextValue>),
}
pub type Fields = BTreeMap<String, TextValue>;

#[derive(Debug, Clone, Serialize, Deserialize, JsonSchema)]
#[serde(rename_all = "camelCase")]
pub struct Localization {
    pub locale: String,
    pub version: String,
    pub heroes: BTreeMap<String, Fields>,
    pub abilities: BTreeMap<String, Fields>,
    pub talents: BTreeMap<String, Fields>,
    pub battlegrounds: BTreeMap<String, Fields>,
    pub units: BTreeMap<String, Fields>,
    pub map_overrides: BTreeMap<String, Value>,
    pub missing: Vec<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize, JsonSchema, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub enum Channel {
    Live,
    Ptr,
}

#[derive(Debug, Clone, Serialize, Deserialize, JsonSchema, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub enum PatchType {
    Live,
    Balance,
    Hotfix,
    Ptr,
    Unknown,
}

#[derive(Debug, Clone, Serialize, Deserialize, JsonSchema, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub enum Availability {
    Available,
    Partial,
    MetadataOnly,
}

#[derive(Debug, Clone, Serialize, Deserialize, JsonSchema)]
#[serde(rename_all = "camelCase")]
pub struct Source {
    pub url: String,
    pub authority: String,
    pub status: String,
}

#[derive(Debug, Clone, Serialize, Deserialize, JsonSchema)]
#[serde(rename_all = "camelCase")]
pub struct PatchSection {
    pub id: String,
    pub heading: String,
    pub level: u8,
    pub hero_id: Option<String>,
    pub battleground_id: Option<String>,
    pub html: String,
    pub text: String,
}

#[derive(Debug, Clone, Serialize, Deserialize, JsonSchema)]
#[serde(rename_all = "camelCase")]
pub struct PatchContent {
    pub locale: String,
    pub html: String,
    pub text: String,
    pub sections: Vec<PatchSection>,
}

#[derive(Debug, Clone, Serialize, Deserialize, JsonSchema)]
#[serde(rename_all = "camelCase")]
pub struct Patch {
    pub id: String,
    pub aliases: Vec<String>,
    pub name: String,
    pub channel: Channel,
    pub patch_type: PatchType,
    pub ptr_state: Option<String>,
    pub cycle_version: Option<String>,
    pub date: String,
    pub build: Option<String>,
    pub sources: Vec<Source>,
    pub availability: Availability,
    pub reason: Option<String>,
    pub contents: BTreeMap<String, PatchContent>,
}

#[derive(Debug, Clone, Serialize, Deserialize, JsonSchema)]
#[serde(rename_all = "camelCase")]
pub struct Asset {
    pub path: String,
    pub sha256: String,
    pub bytes: u64,
}

#[derive(Debug, Clone, Serialize, Deserialize, JsonSchema)]
#[serde(rename_all = "camelCase")]
pub struct Manifest {
    pub schema_version: u32,
    pub dataset_version: String,
    pub game_version: String,
    pub locales: Vec<String>,
    pub counts: BTreeMap<String, usize>,
    pub missing_translations: BTreeMap<String, usize>,
    pub sources: BTreeMap<String, String>,
    pub files: BTreeMap<String, String>,
}
