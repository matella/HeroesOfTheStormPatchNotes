use crate::{
    http::Http,
    model::*,
    normalize::{clean_html, plain},
};
use anyhow::{Context, Result, ensure};
use regex::Regex;
use scraper::{ElementRef, Html, Selector};
use serde_json::Value;
use std::collections::{BTreeMap, BTreeSet};
use url::Url;

const INDEX: &str = "https://raw.githubusercontent.com/heroespatchnotes/heroes-patch-data";
fn selector(s: &str) -> Selector {
    Selector::parse(s).expect("static selector")
}
fn canonical(url: &str) -> Option<String> {
    let u = Url::parse(url).ok()?;
    let allowed = [
        "us.battle.net",
        "eu.battle.net",
        "heroesofthestorm.com",
        "news.blizzard.com",
    ];
    if !allowed.contains(&u.host_str()?) {
        return None;
    }
    let r = Regex::new(r"/(?:blog|article|heroes-of-the-storm)/(\d+)").expect("regex");
    r.captures(url)
        .map(|c| format!("https://news.blizzard.com/en-us/article/{}", &c[1]))
}
fn identity(build: Option<&str>, channel: &Channel, fallback: &str) -> String {
    match build {
        Some(b) => format!(
            "{}:{b}",
            if *channel == Channel::Live {
                "live"
            } else {
                "ptr"
            }
        ),
        None => format!("source:{fallback}"),
    }
}
fn metadata(row: &Value) -> Result<Patch> {
    let alias = row["internalId"]
        .as_str()
        .context("patch internalId missing")?;
    let build = row["liveBuild"].as_str().map(str::to_owned);
    let mut sources = Vec::new();
    for field in ["officialLink", "alternateLink"] {
        if let Some(url) = row[field].as_str() {
            sources.push(Source {
                url: canonical(url).unwrap_or_else(|| url.into()),
                authority: if field == "officialLink" {
                    "official"
                } else {
                    "community"
                }
                .into(),
                status: "notFetched".into(),
            });
        }
    }
    Ok(Patch {
        id: identity(build.as_deref(), &Channel::Live, alias),
        aliases: vec![alias.into()],
        name: row["patchName"].as_str().unwrap_or(alias).into(),
        channel: Channel::Live,
        ptr_state: None,
        cycle_version: None,
        patch_type: match row["patchType"].as_str() {
            Some("Patch Notes") => PatchType::Live,
            Some("Balance Update") => PatchType::Balance,
            Some("Hotfix Patch") => PatchType::Hotfix,
            _ => PatchType::Unknown,
        },
        date: row["liveDate"]
            .as_str()
            .context("patch date missing")?
            .into(),
        build,
        sources,
        availability: Availability::MetadataOnly,
        reason: Some("No validated article content acquired".into()),
        contents: BTreeMap::new(),
    })
}

/// Reconcile strong build identity or an explicitly recorded source alias only.
pub fn merge(records: Vec<Patch>) -> Result<Vec<Patch>> {
    let mut result: BTreeMap<String, Patch> = BTreeMap::new();
    for mut p in records {
        if let Some(existing) = result.get_mut(&p.id) {
            ensure!(
                existing.channel == p.channel
                    && (existing.build.is_none() || p.build.is_none() || existing.build == p.build),
                "conflicting patch identity"
            );
            if existing.build.is_none() {
                existing.build = p.build.clone();
            }
            existing.aliases.append(&mut p.aliases);
            existing.aliases.sort();
            existing.aliases.dedup();
            for s in p.sources {
                if !existing.sources.iter().any(|x| x.url == s.url) {
                    existing.sources.push(s);
                }
            }
            existing.contents.extend(p.contents);
            if !existing.contents.is_empty() {
                existing.availability = Availability::Available;
                existing.reason = None;
            }
        } else {
            result.insert(p.id.clone(), p);
        }
    }
    Ok(result.into_values().collect())
}

pub fn parse_article(
    html: &str,
    locale: &str,
    game: &GameData,
    localization: &Localization,
) -> Result<PatchContent> {
    let doc = Html::parse_document(html);
    let article = [
        ".TopicPost--blizzard .TopicPost-bodyContent",
        ".post-content",
        "article",
        "[itemprop=text]",
    ]
    .into_iter()
    .find_map(|s| doc.select(&selector(s)).next())
    .context("article body not found")?;
    let raw = article.inner_html();
    let body = clean_html(&raw);
    let text = plain(&body);
    ensure!(text.len() > 40, "article body is empty or placeholder");
    // Walk headings in document order, preserving nested lists and paragraph markup.
    let mut sections: Vec<PatchSection> = Vec::new();
    let mut names: BTreeMap<String, String> = localization
        .heroes
        .iter()
        .filter_map(|(id, f)| match f.get("name") {
            Some(TextValue::Text { text, .. }) => Some((name_key(text), id.clone())),
            _ => None,
        })
        .collect();
    for hero in game.heroes.values() {
        for alias in [&hero.id, &hero.hyperlink_id] {
            names
                .entry(name_key(alias))
                .or_insert_with(|| hero.id.clone());
        }
    }
    let maps: BTreeMap<String, String> = game
        .battlegrounds
        .values()
        .map(|m| (name_key(&m.source_key), m.id.clone()))
        .collect();
    fn walk(
        el: ElementRef<'_>,
        sections: &mut Vec<PatchSection>,
        names: &BTreeMap<String, String>,
        maps: &BTreeMap<String, String>,
        contexts: &mut Vec<(u8, Option<String>, Option<String>)>,
    ) {
        let tag = el.value().name();
        if ["h1", "h2", "h3", "h4", "h5", "h6"].contains(&tag) {
            let heading = el.text().collect::<String>();
            let level: u8 = tag[1..].parse().unwrap_or(1);
            while contexts.last().is_some_and(|c| c.0 >= level) {
                contexts.pop();
            }
            let inherited = contexts.last().cloned();
            let hero_id = names
                .get(&name_key(&heading))
                .cloned()
                .or_else(|| inherited.as_ref().and_then(|c| c.1.clone()));
            let map_id = maps
                .get(&name_key(&heading))
                .cloned()
                .or_else(|| inherited.as_ref().and_then(|c| c.2.clone()));
            contexts.push((level, hero_id.clone(), map_id.clone()));
            sections.push(PatchSection {
                id: format!("section-{}", sections.len()),
                heading: heading.clone(),
                level,
                hero_id,
                battleground_id: map_id,
                html: String::new(),
                text: String::new(),
            });
        } else if ["p", "ul", "ol", "table", "blockquote"].contains(&tag) {
            if sections.is_empty() {
                sections.push(PatchSection {
                    id: "section-0".into(),
                    heading: "General".into(),
                    level: 1,
                    hero_id: None,
                    battleground_id: None,
                    html: String::new(),
                    text: String::new(),
                });
            }
            let s = sections.last_mut().expect("section");
            s.html.push_str(&clean_html(&el.html()));
        } else {
            for child in el.children().filter_map(ElementRef::wrap) {
                walk(child, sections, names, maps, contexts);
            }
        }
    }
    walk(article, &mut sections, &names, &maps, &mut Vec::new());
    for s in &mut sections {
        s.text = plain(&s.html);
    }
    Ok(PatchContent {
        locale: locale.into(),
        html: body,
        text,
        sections,
    })
}
fn name_key(s: &str) -> String {
    s.chars()
        .filter(|c| c.is_alphanumeric())
        .flat_map(char::to_lowercase)
        .collect()
}

#[derive(Clone)]
struct Discovery {
    alias: String,
    title: String,
    date: String,
    url: String,
    channel: Channel,
}
fn discover(http: &Http) -> Result<Vec<Discovery>> {
    let mut found = BTreeMap::new();
    let patch_words = Regex::new(r"(?i)patch notes|balance patch|hotfix").expect("regex");
    for page in 1..=50 {
        let url = if page == 1 {
            "https://www.bluetracker.gg/heroes/".into()
        } else {
            format!("https://www.bluetracker.gg/heroes/?page={page}")
        };
        let html = String::from_utf8(http.get(&url, false)?)?;
        let doc = Html::parse_document(&html);
        ensure!(
            doc.select(&selector("table.topic-listing"))
                .next()
                .is_some(),
            "BlueTracker listing layout changed"
        );
        let rows: Vec<_> = doc
            .select(&selector("table.topic-listing tbody tr"))
            .collect();
        if rows.is_empty() {
            break;
        }
        for row in rows {
            let Some(link) = row.select(&selector("a.topic-link")).next() else {
                continue;
            };
            let title = link
                .value()
                .attr("title")
                .map(str::to_owned)
                .unwrap_or_else(|| link.text().collect());
            if !patch_words.is_match(&title) {
                continue;
            }
            let href = link.value().attr("href").context("missing topic href")?;
            // The US English blog mirror contains full articles; retain forum mirrors only
            // where no blog alias exists. Regional aliases are not translated artificially.
            if !href.contains("/us-en/") {
                continue;
            }
            let url = Url::parse("https://www.bluetracker.gg")?
                .join(href)?
                .to_string();
            let topic = href
                .trim_end_matches('/')
                .rsplit('/')
                .next()
                .context("invalid topic")?;
            let number = topic.split('-').next().context("topic ID missing")?;
            ensure!(
                number.bytes().all(|b| b.is_ascii_digit()) && !number.is_empty(),
                "invalid topic ID"
            );
            let alias = format!("bluetracker:us-en:{number}");
            let date = topic_date(&title).or_else(|| {
                row.select(&selector("time"))
                    .next()
                    .and_then(|t| t.value().attr("datetime"))
                    .map(|d| d[..10.min(d.len())].to_string())
            });
            let Some(date) = date else {
                anyhow::bail!("cannot determine date for {title}")
            };
            let channel = if title.to_lowercase().contains("ptr") {
                Channel::Ptr
            } else {
                Channel::Live
            };
            found.insert(
                alias.clone(),
                Discovery {
                    alias,
                    title,
                    date,
                    url,
                    channel,
                },
            );
        }
        // Pagination is driven by the page links, not by whether this page had patches.
        let next = format!("?page={}", page + 1);
        if !doc
            .select(&selector("a[href]"))
            .any(|a| a.value().attr("href") == Some(next.as_str()))
        {
            break;
        }
        ensure!(page < 50, "BlueTracker pagination exceeds safety bound");
    }
    ensure!(!found.is_empty(), "no patch topics discovered");
    Ok(found.into_values().collect())
}
fn topic_date(title: &str) -> Option<String> {
    let r=Regex::new(r"(?i)(January|February|March|April|May|June|July|August|September|October|November|December)\s+(\d{1,2}),?\s+(\d{4})").ok()?;
    let c = r.captures(title)?;
    chrono::NaiveDate::parse_from_str(&format!("{} {} {}", &c[1], &c[2], &c[3]), "%B %d %Y")
        .ok()
        .map(|d| d.to_string())
}
fn build_from_article(html: &str) -> Option<String> {
    let r = Regex::new(r"\b\d+\.\d+\.\d+\.(\d{4,6})\b").ok()?;
    let mut values: BTreeSet<_> = r.captures_iter(html).map(|v| v[1].to_string()).collect();
    if values.len() == 1 {
        values.pop_first()
    } else {
        None
    }
}
fn source_article(http: &Http, url: &str, immutable: bool) -> Result<(String, String)> {
    let body = String::from_utf8(http.get(url, immutable)?)?;
    let doc = Html::parse_document(&body);
    if let Some(target) = doc
        .select(&selector(".post-link a[href]"))
        .filter_map(|a| a.value().attr("href"))
        .find_map(canonical)
    {
        return Ok((
            target.clone(),
            String::from_utf8(http.get(&target, false)?)?,
        ));
    }
    if let Some(link) = doc.select(&selector(".post-content a[href]")).find(|a| {
        a.text()
            .collect::<String>()
            .to_lowercase()
            .contains("view full article")
    }) {
        let target = link
            .value()
            .attr("href")
            .context("missing official article URL")?;
        if let Some(url) = canonical(target) {
            return Ok((url.clone(), String::from_utf8(http.get(&url, false)?)?));
        }
    }
    Ok((url.into(), body))
}

pub fn acquire(
    http: &Http,
    index_sha: &str,
    game: &GameData,
    en: &Localization,
    history: &Value,
    previous: &[Patch],
    ptr: &crate::upstream::PtrEvidence,
) -> Result<Vec<Patch>> {
    let input = http.json(&format!("{INDEX}/{index_sha}/patchversions.json"), true)?;
    let rows = input.as_array().context("patch index must be array")?;
    let mut records = rows.iter().map(metadata).collect::<Result<Vec<_>>>()?;
    let prior: BTreeMap<_, _> = previous.iter().map(|p| (p.id.as_str(), p)).collect();
    // Bootstrap official archives/legacy metadata. The current path never copies Nexus.
    for p in &mut records {
        if let Some(cached) = prior.get(p.id.as_str()) {
            p.contents = cached.contents.clone();
            p.availability = cached.availability.clone();
            p.reason = cached.reason.clone();
        }
        let alias = &p.aliases[0];
        if let Some(curated) = history.get(alias) {
            if let Some(date) = curated["date"].as_str() {
                p.date = date.into();
            }
            if let Some(url) = curated["url"].as_str() {
                p.sources.push(Source {
                    url: url.into(),
                    authority: curated["authority"].as_str().unwrap_or("unknown").into(),
                    status: "metadataOnly".into(),
                });
            }
        }
    }
    // Concurrency keeps one-time historical acquisition practical; ordering is normalized later.
    let jobs: Vec<_> = records
        .iter()
        .enumerate()
        .map(|(i, p)| {
            let curated = history.get(&p.aliases[0]);
            let override_url = curated
                .filter(|c| c["allowContent"] == true)
                .and_then(|c| c["url"].as_str());
            let url = override_url.map(str::to_owned).or_else(|| {
                p.sources
                    .iter()
                    .find(|s| {
                        s.authority == "official"
                            && (s.url.starts_with("https://news.blizzard.com/")
                                || s.url.starts_with("https://us.forums.blizzard.com/"))
                    })
                    .map(|s| s.url.clone())
            });
            (i, url)
        })
        .filter_map(|(i, u)| u.map(|u| (i, u)))
        .collect();
    let updates = std::sync::Mutex::new(Vec::new());
    std::thread::scope(|scope| {
        for chunk in jobs.chunks((jobs.len() / 8).max(1)) {
            let updates = &updates;
            scope.spawn(move || {
                for (i, url) in chunk {
                    let outcome =
                        source_article(http, url, url.starts_with("https://web.archive.org/"))
                            .and_then(|(source, html)| {
                                Ok((source, parse_article(&html, "enus", game, en)?))
                            });
                    updates.lock().expect("update lock").push((*i, outcome));
                }
            });
        }
    });
    for (i, outcome) in updates.into_inner().expect("update lock") {
        let p = &mut records[i];
        match outcome {
            Ok((url, content)) => {
                p.contents.insert("enus".into(), content);
                p.availability = Availability::Available;
                p.reason = None;
                for s in &mut p.sources {
                    if s.url == url {
                        s.status = "available".into();
                    }
                }
            }
            Err(error) => {
                ensure!(
                    p.contents.is_empty(),
                    "previously available article failed revalidation: {error:#}"
                );
                p.reason =
                    Some("Historical source unavailable or unvalidated; metadata retained".into());
            }
        }
    }
    let topics = discover(http)?;
    let latest_ptr = topics
        .iter()
        .filter(|t| t.channel == Channel::Ptr)
        .map(|t| t.date.as_str())
        .max();
    for t in topics
        .iter()
        .filter(|t| t.channel == Channel::Live || Some(t.date.as_str()) == latest_ptr)
    {
        let prior_alias = previous.iter().find(|p| p.aliases.contains(&t.alias));
        let cycle = if t.channel == Channel::Ptr {
            ptr_cycle(
                &t.date,
                prior_alias.and_then(|p| p.cycle_version.as_deref()),
                ptr,
            )
        } else {
            None
        };
        if t.channel == Channel::Ptr
            && cycle
                .as_deref()
                .is_some_and(|c| release_line(&game.version) >= release_line(c))
        {
            continue;
        }
        let (source, html) = source_article(http, &t.url, false)?;
        let content = parse_article(&html, "enus", game, en)
            .with_context(|| format!("current article {}", t.url))?;
        let official = canonical(&source);
        let build = build_from_article(&html);
        // Match an existing alias/article only when this is not a conflicting known build.
        let known = records.iter().find(|p| {
            p.aliases.contains(&t.alias)
                || (p.channel == t.channel
                    && p.date == t.date
                    && p.sources.iter().any(|s| Some(s.url.clone()) == official)
                    && (build.is_none() || p.build == build))
        });
        let id = prior_alias
            .or(known)
            .map(|p| p.id.clone())
            .unwrap_or_else(|| identity(build.as_deref(), &t.channel, &t.alias));
        let patch_type = if t.channel == Channel::Ptr {
            PatchType::Ptr
        } else if t.title.to_lowercase().contains("hotfix") {
            PatchType::Hotfix
        } else if t.title.to_lowercase().contains("balance") {
            PatchType::Balance
        } else {
            PatchType::Live
        };
        records.push(Patch {
            id,
            aliases: vec![t.alias.clone()],
            name: t.title.clone(),
            channel: t.channel.clone(),
            patch_type,
            ptr_state: if t.channel == Channel::Ptr {
                Some("candidate".into())
            } else {
                None
            },
            cycle_version: cycle,
            date: t.date.clone(),
            build: prior_alias
                .or(known)
                .and_then(|p| p.build.clone())
                .or(build),
            sources: vec![Source {
                url: source,
                authority: if official.is_some() {
                    "official"
                } else {
                    "officialMirror"
                }
                .into(),
                status: "available".into(),
            }],
            availability: Availability::Available,
            reason: None,
            contents: BTreeMap::from([("enus".into(), content)]),
        });
    }
    reconcile(records, previous)
}
fn reconcile(records: Vec<Patch>, previous: &[Patch]) -> Result<Vec<Patch>> {
    // Blog/forum mirror aliases of the same article/date are reconciled explicitly.
    let mut merged = merge(records)?;
    let mut aliases: BTreeMap<(String, String, ChannelKey), String> = BTreeMap::new();
    let mut remap = BTreeMap::new();
    let published_ids: BTreeSet<_> = previous.iter().map(|p| p.id.as_str()).collect();
    let mut preferred: Vec<_> = merged.iter().collect();
    preferred.sort_by_key(|p| (!published_ids.contains(p.id.as_str()), p.id.clone()));
    for p in preferred {
        for s in &p.sources {
            if let Some(article) = canonical(&s.url) {
                let key = (
                    article,
                    p.date.clone(),
                    ChannelKey(p.channel == Channel::Ptr),
                );
                if let Some(id) = aliases.get(&key) {
                    let other = merged.iter().find(|x| &x.id == id).expect("known ID");
                    if p.build == other.build || p.build.is_none() || other.build.is_none() {
                        remap.insert(p.id.clone(), id.clone());
                    }
                } else {
                    aliases.insert(key, p.id.clone());
                }
            }
        }
    }
    for p in &mut merged {
        if let Some(id) = remap.get(&p.id) {
            p.id = id.clone();
        }
    }
    merge(merged)
}
fn release_line(version: &str) -> Vec<u32> {
    version
        .trim_end_matches("_ptr")
        .split('.')
        .take(3)
        .filter_map(|s| s.parse().ok())
        .collect()
}
fn ptr_cycle(
    date: &str,
    prior: Option<&str>,
    evidence: &crate::upstream::PtrEvidence,
) -> Option<String> {
    if let Some(c) = prior {
        return Some(c.into());
    }
    let nearby = evidence
        .first_extracted_date
        .as_deref()
        .and_then(|first| {
            let announced = chrono::NaiveDate::parse_from_str(date, "%Y-%m-%d").ok()?;
            let extracted = chrono::NaiveDate::parse_from_str(first, "%Y-%m-%d").ok()?;
            Some((extracted - announced).num_days())
        })
        .is_some_and(|days| (0..=3).contains(&days));
    if nearby {
        evidence
            .latest_version
            .as_ref()
            .map(|v| v.split('.').take(3).collect::<Vec<_>>().join("."))
    } else {
        None
    }
}
#[derive(PartialEq, Eq, PartialOrd, Ord)]
struct ChannelKey(bool);

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn dated_titles_and_article_rewrite() {
        assert_eq!(
            topic_date("Hotfix Patch Notes - November 9, 2026").unwrap(),
            "2026-11-09"
        );
        assert_eq!(
            canonical("http://us.battle.net/heroes/en/blog/123"),
            Some("https://news.blizzard.com/en-us/article/123".into())
        );
        assert!(canonical("https://evil.test/article/123").is_none());
    }
    #[test]
    fn distinct_builds_same_day_are_distinct() {
        assert_ne!(
            identity(Some("10"), &Channel::Live, "a"),
            identity(Some("11"), &Channel::Live, "a")
        );
    }
    #[test]
    fn missing_metadata_is_not_a_fake_body() {
        let p = metadata(
            &serde_json::json!({"internalId":"hotfix","liveBuild":"10","liveDate":"2020-01-01"}),
        )
        .unwrap();
        assert!(p.contents.is_empty());
        assert_eq!(p.availability, Availability::MetadataOnly);
    }
}

#[cfg(test)]
mod relationship_tests {
    use super::*;
    use crate::fixtures;
    #[test]
    fn headings_inherit_hero_and_clear_at_peer_boundaries() {
        let game = fixtures::game();
        let en = fixtures::locale("enus");
        let html = "<article><h3>Heroes</h3><h4>Hero</h4><h5>Talents</h5><ul><li>A sufficiently detailed synthetic talent correction.</li></ul><h4>General</h4><p>Another synthetic general correction for the release.</p></article>";
        let content = parse_article(html, "enus", &game, &en).unwrap();
        let talent = content
            .sections
            .iter()
            .find(|s| s.heading == "Talents")
            .unwrap();
        assert_eq!(talent.hero_id.as_deref(), Some("Hero"));
        assert!(!talent.text.is_empty());
        assert!(
            content
                .sections
                .iter()
                .find(|s| s.heading == "General")
                .unwrap()
                .hero_id
                .is_none()
        );
    }
    #[test]
    fn builds_do_not_merge_but_aliases_can_be_enriched() {
        let first = fixtures::patch();
        let mut second = fixtures::patch();
        second.id = "live:11".into();
        second.build = Some("11".into());
        assert_eq!(merge(vec![first.clone(), second]).unwrap().len(), 2);
        let mut alias = first.clone();
        alias.build = None;
        alias.aliases = vec!["mirror".into()];
        let merged = merge(vec![first, alias]).unwrap();
        assert_eq!(merged.len(), 1);
        assert_eq!(merged[0].aliases, vec!["mirror", "original"]);
    }
}

#[cfg(test)]
mod ptr_tests {
    use super::*;
    #[test]
    fn cold_cutover_has_evidence_but_a_new_unextracted_cycle_stays_candidate() {
        let evidence = crate::upstream::PtrEvidence {
            active_version: None,
            latest_version: Some("2.1.0.100_ptr".into()),
            first_extracted_date: Some("2026-01-10".into()),
        };
        assert_eq!(
            ptr_cycle("2026-01-09", None, &evidence),
            Some("2.1.0".into())
        );
        assert!(
            release_line("2.1.0.101")
                >= release_line(&ptr_cycle("2026-01-09", None, &evidence).unwrap())
        );
        assert_eq!(ptr_cycle("2026-02-01", None, &evidence), None);
        assert_eq!(ptr_cycle("2025-12-01", None, &evidence), None);
        assert_eq!(
            ptr_cycle("2026-02-01", Some("2.2.0"), &evidence),
            Some("2.2.0".into())
        );
    }
}

#[cfg(test)]
mod canonical_identity_tests {
    use super::*;
    #[test]
    fn new_build_mirror_cannot_replace_a_published_source_id() {
        let mut published = crate::fixtures::patch();
        published.id = "source:stable".into();
        published.build = None;
        published.sources = vec![Source {
            url: "https://news.blizzard.com/en-us/article/123".into(),
            authority: "official".into(),
            status: "available".into(),
        }];
        let mut mirror = published.clone();
        mirror.id = "live:10".into();
        mirror.build = Some("10".into());
        mirror.aliases = vec!["new-mirror".into()];
        let output = reconcile(vec![mirror, published.clone()], &[published]).unwrap();
        assert_eq!(output.len(), 1);
        assert_eq!(output[0].id, "source:stable");
        assert_eq!(output[0].build.as_deref(), Some("10"));
    }
}
