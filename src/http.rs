use anyhow::{Context, Result, bail, ensure};
use reqwest::blocking::Client;
use serde_json::Value;
use sha2::{Digest, Sha256};
use std::{
    fs,
    io::Read,
    path::{Component, Path, PathBuf},
    sync::{Arc, Mutex},
    time::Duration,
};
use url::Url;

const HOSTS: [&str; 6] = [
    "api.github.com",
    "raw.githubusercontent.com",
    "www.bluetracker.gg",
    "news.blizzard.com",
    "us.forums.blizzard.com",
    "web.archive.org",
];
fn allowed(url: &Url) -> bool {
    url.scheme() == "https"
        && url.username().is_empty()
        && url.password().is_none()
        && url.port_or_known_default() == Some(443)
        && url.host_str().is_some_and(|h| HOSTS.contains(&h))
}
pub fn digest(bytes: &[u8]) -> String {
    format!("{:x}", Sha256::digest(bytes))
}
pub fn safe_path(value: &str) -> Result<()> {
    ensure!(
        !value.is_empty() && !value.contains('\\') && !value.contains('\0'),
        "invalid relative path: {value}"
    );
    ensure!(
        Path::new(value)
            .components()
            .all(|c| matches!(c, Component::Normal(_))),
        "unsafe path: {value}"
    );
    Ok(())
}

type ResponseSlot = Arc<Mutex<Option<Vec<u8>>>>;
#[derive(Clone)]
pub struct Http {
    client: Client,
    cache: PathBuf,
    responses: Arc<Mutex<std::collections::BTreeMap<String, ResponseSlot>>>,
    pub inputs: Arc<Mutex<std::collections::BTreeMap<String, String>>>,
}
impl Http {
    pub fn new(cache: &Path) -> Result<Self> {
        fs::create_dir_all(cache)?;
        let client = Client::builder()
            .user_agent("hots-reference/0.1 (reference-data producer)")
            .timeout(Duration::from_secs(60))
            .connect_timeout(Duration::from_secs(20))
            .redirect(reqwest::redirect::Policy::custom(|attempt| {
                if attempt.previous().len() >= 8 {
                    attempt.error("too many redirects")
                } else if !allowed(attempt.url()) {
                    attempt.error("redirect outside approved HTTPS origins")
                } else {
                    attempt.follow()
                }
            }))
            .build()?;
        Ok(Self {
            client,
            cache: cache.into(),
            responses: Default::default(),
            inputs: Default::default(),
        })
    }
    pub fn get(&self, url: &str, immutable: bool) -> Result<Vec<u8>> {
        let slot = self
            .responses
            .lock()
            .expect("response slots")
            .entry(url.into())
            .or_insert_with(|| Arc::new(Mutex::new(None)))
            .clone();
        let mut value = slot.lock().expect("response slot");
        if let Some(bytes) = value.as_ref() {
            return Ok(bytes.clone());
        }
        let bytes = self.fetch(url, immutable)?;
        *value = Some(bytes.clone());
        Ok(bytes)
    }
    fn fetch(&self, url: &str, immutable: bool) -> Result<Vec<u8>> {
        let parsed = Url::parse(url)?;
        ensure!(allowed(&parsed), "source outside approved HTTPS origins");
        let host = parsed.host_str().context("missing URL host")?;
        let key = digest(url.as_bytes());
        let path = self.cache.join(&key);
        let bytes = if immutable && path.exists() {
            fs::read(&path)?
        } else {
            let mut request = self.client.get(url);
            if host == "api.github.com"
                && let Ok(token) = std::env::var("GITHUB_TOKEN")
            {
                request = request.bearer_auth(token);
            }
            let response = request.send().with_context(|| format!("fetch {url}"))?;
            if !response.status().is_success() {
                bail!("source HTTP {}: {url}", response.status());
            }
            let mut bytes = Vec::new();
            response
                .take(64 * 1024 * 1024 + 1)
                .read_to_end(&mut bytes)?;
            ensure!(
                bytes.len() <= 64 * 1024 * 1024,
                "source exceeds size limit: {url}"
            );
            ensure!(!bytes.is_empty(), "empty source: {url}");
            static SERIAL: std::sync::atomic::AtomicU64 = std::sync::atomic::AtomicU64::new(0);
            let seq = SERIAL.fetch_add(1, std::sync::atomic::Ordering::Relaxed);
            let temp = self
                .cache
                .join(format!("{key}.{}.{seq}.tmp", std::process::id()));
            fs::write(&temp, &bytes)?;
            fs::rename(temp, &path)?;
            bytes
        };
        self.inputs
            .lock()
            .expect("input lock")
            .insert(url.into(), digest(&bytes));
        Ok(bytes)
    }
    pub fn json(&self, url: &str, immutable: bool) -> Result<Value> {
        serde_json::from_slice(&self.get(url, immutable)?).with_context(|| format!("JSON {url}"))
    }
    pub fn pin(&self, repo: &str) -> Result<String> {
        let v = self.json(
            &format!("https://api.github.com/repos/{repo}/commits?per_page=1"),
            false,
        )?;
        let sha = v[0]["sha"].as_str().context("missing commit SHA")?;
        ensure!(
            sha.len() == 40 && sha.bytes().all(|b| b.is_ascii_hexdigit()),
            "invalid commit SHA"
        );
        Ok(sha.into())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn origin_policy_rejects_redirect_downgrades_and_credentials() {
        for u in [
            "http://news.blizzard.com/a",
            "https://news.blizzard.com.evil.test/a",
            "https://token@news.blizzard.com/a",
            "https://news.blizzard.com:444/a",
        ] {
            assert!(!allowed(&Url::parse(u).unwrap()));
        }
        assert!(allowed(
            &Url::parse("https://news.blizzard.com/en-us/article/1").unwrap()
        ));
    }
}
