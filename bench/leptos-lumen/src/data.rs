// The shared fixtures (bench/lumen-shared/fixtures.json), compiled into both the server and the WebAssembly build: the
// browser renders client-side navigations with the same lookups. Issues are mutable on the server only.
use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use std::sync::LazyLock;

#[derive(Deserialize)]
pub struct Status {
    pub key: String,
    pub name: String,
    pub color: String,
}

#[derive(Deserialize)]
pub struct Person {
    pub name: String,
    pub initials: String,
    pub color: String,
}

#[derive(Deserialize)]
pub struct Label {
    pub name: String,
    pub color: String,
}

#[derive(Deserialize)]
pub struct Notification {
    pub icon: String,
    pub color: String,
    pub who: String,
    pub text: String,
    pub issue: String,
    pub at: String,
    pub unread: bool,
}

#[derive(Clone, Debug, PartialEq, Serialize, Deserialize)]
pub struct Issue {
    pub id: String,
    pub title: String,
    pub description: String,
    pub status: String,
    pub priority: String,
    pub assignee: String,
    pub labels: Vec<String>,
    pub team: String,
    pub updated: String,
    #[serde(default)]
    pub updated_text: String,
}

#[derive(Deserialize)]
pub struct Fixtures {
    pub statuses: Vec<Status>,
    pub priorities: serde_json::Map<String, serde_json::Value>,
    pub people: Vec<Person>,
    pub issues: Vec<Issue>,
    pub notifications: Vec<Notification>,
    pub labels: Vec<Label>,
    pub icons: HashMap<String, String>,
}

pub static FIX: LazyLock<Fixtures> =
    LazyLock::new(|| serde_json::from_str(include_str!("../../lumen-shared/fixtures.json")).expect("fixtures"));

pub fn fix() -> &'static Fixtures {
    &FIX
}

pub fn icon(kind: &str) -> &'static str {
    fix().icons.get(kind).map(String::as_str).unwrap_or("")
}

pub fn status(key: &str) -> &'static Status {
    fix().statuses.iter().find(|s| s.key == key).expect("status")
}

pub fn person(name: &str) -> &'static Person {
    fix().people.iter().find(|p| p.name == name).expect("person")
}

pub fn label(name: &str) -> &'static Label {
    fix().labels.iter().find(|l| l.name == name).expect("label")
}

pub fn priorities() -> impl Iterator<Item = (&'static str, &'static str)> {
    fix().priorities.iter().map(|(k, v)| (k.as_str(), v.as_str().unwrap_or("")))
}

impl Issue {
    pub fn href(&self) -> String {
        format!("/issues/{}", self.id)
    }
    pub fn status_icon(&self) -> String {
        format!("status-{}", self.status)
    }
    pub fn status_color(&self) -> &'static str {
        &status(&self.status).color
    }
    pub fn priority_icon(&self) -> String {
        format!("prio-{}", self.priority)
    }
    pub fn priority_color(&self) -> &'static str {
        if self.priority == "urgent" { "#F2994A" } else { "#8B8F98" }
    }
    pub fn person(&self) -> &'static Person {
        person(&self.assignee)
    }
}

fn days_from_civil(y: i64, m: i64, d: i64) -> i64 {
    let y = if m <= 2 { y - 1 } else { y };
    let era = y.div_euclid(400);
    let yoe = y - era * 400;
    let doy = (153 * (if m > 2 { m - 3 } else { m + 9 }) + 2) / 5 + d - 1;
    let doe = yoe * 365 + yoe / 4 - yoe / 100 + doy;
    era * 146097 + doe - 719468
}

fn minutes(t: &str) -> i64 {
    let n = |a: usize, b: usize| t.get(a..b).and_then(|s| s.parse::<i64>().ok()).unwrap_or(0);
    days_from_civil(n(0, 4), n(5, 7), n(8, 10)) * 1440 + n(11, 13) * 60 + n(14, 16)
}

/// "5m ago", "3h ago" or "2d ago" against the fixtures' fixed now (2026-10-10 12:00), as every variant computes it.
pub fn ago(t: &str) -> String {
    let m = (minutes("2026-10-10T12:00:00") - minutes(t)).max(0);
    if m < 60 { format!("{m}m ago") } else if m < 1440 { format!("{}h ago", m / 60) } else { format!("{}d ago", m / 1440) }
}

#[cfg(feature = "ssr")]
pub mod store {
    use super::{ago, fix, Issue};
    use std::sync::{Arc, LazyLock, RwLock};
    use std::time::{SystemTime, UNIX_EPOCH};

    static ISSUES: LazyLock<RwLock<Arc<Vec<Issue>>>> = LazyLock::new(|| {
        RwLock::new(Arc::new(fix().issues.iter().cloned().map(|mut i| { i.updated_text = ago(&i.updated); i }).collect()))
    });

    pub fn all() -> Arc<Vec<Issue>> {
        ISSUES.read().expect("issues").clone()
    }

    pub fn find(id: &str) -> Option<Issue> {
        all().iter().find(|i| i.id == id).cloned()
    }

    fn now() -> String {
        let s = SystemTime::now().duration_since(UNIX_EPOCH).map(|d| d.as_secs() as i64).unwrap_or(0);
        let (days, rem) = (s.div_euclid(86400), s.rem_euclid(86400));
        let z = days + 719468;
        let era = z.div_euclid(146097);
        let doe = z - era * 146097;
        let yoe = (doe - doe / 1460 + doe / 36524 - doe / 146096) / 365;
        let doy = doe - (365 * yoe + yoe / 4 - yoe / 100);
        let mp = (5 * doy + 2) / 153;
        let d = doy - (153 * mp + 2) / 5 + 1;
        let m = if mp < 10 { mp + 3 } else { mp - 9 };
        let y = yoe + era * 400 + i64::from(m <= 2);
        format!("{y:04}-{m:02}-{d:02}T{:02}:{:02}:{:02}", rem / 3600, rem % 3600 / 60, rem % 60)
    }

    pub fn update(id: &str, f: impl Fn(&mut Issue)) {
        let mut guard = ISSUES.write().expect("issues");
        let mut next = (**guard).clone();
        if let Some(i) = next.iter_mut().find(|i| i.id == id) {
            f(i);
            i.updated = now();
            i.updated_text = ago(&i.updated);
        }
        *guard = Arc::new(next);
    }
}
