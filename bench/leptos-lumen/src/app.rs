// Lumen's pages, the same markup as bench/next-lumen: rendered on the server (SsrMode::Async: the data is in memory, so
// every page is complete when it is sent), hydrated in the browser, and navigated client-side by leptos_router with the
// data fetched through server functions. Saving the issue form is an ActionForm on a server function.
use crate::data::{self, fix, Issue};
use crate::feed::Snapshot;
use leptos::prelude::*;
use leptos_meta::{provide_meta_context, MetaTags, Stylesheet, Title};
use leptos_router::components::{Route, Router, Routes};
use leptos_router::hooks::{use_location, use_params_map};
use leptos_router::{path, SsrMode};
use serde::{Deserialize, Serialize};

pub fn shell(options: LeptosOptions) -> impl IntoView {
    let theme = if is_dark() { "dark" } else { "light" };
    view! {
        <!DOCTYPE html>
        <html lang="en" data-theme=theme>
            <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1" />
                <HydrationScripts options />
                <MetaTags />
            </head>
            <body>
                <App />
            </body>
        </html>
    }
}

#[cfg(feature = "ssr")]
pub fn cookie_dark(cookie: Option<&str>) -> bool {
    cookie.is_some_and(|c| c.split(';').any(|p| p.trim() == "theme=dark"))
}

fn is_dark() -> bool {
    #[cfg(feature = "ssr")]
    {
        use_context::<axum::http::request::Parts>()
            .is_some_and(|p| cookie_dark(p.headers.get("cookie").and_then(|v| v.to_str().ok())))
    }
    #[cfg(not(feature = "ssr"))]
    {
        document().document_element().and_then(|e| e.get_attribute("data-theme")).as_deref() == Some("dark")
    }
}

#[derive(Clone, Copy)]
struct ShellState {
    dark: bool,
    drawer: RwSignal<bool>,
}

#[component]
pub fn App() -> impl IntoView {
    provide_meta_context();
    let shell = ShellState { dark: is_dark(), drawer: RwSignal::new(false) };
    provide_context(shell);
    view! {
        <Stylesheet id="leptos" href="/pkg/leptos-lumen.css" />
        <Title text="Lumen" />
        <Router>
            <Shell>
                <Routes fallback=|| view! { <div style="padding:28px">"Not found."</div> }>
                    <Route path=path!("/issues") view=IssuesPage ssr=SsrMode::Async />
                    <Route path=path!("/issues/:id") view=IssuePage ssr=SsrMode::Async />
                    <Route path=path!("/projects") view=ProjectsPage ssr=SsrMode::Async />
                    <Route path=path!("/inbox") view=InboxPage ssr=SsrMode::Async />
                    <Route path=path!("/settings") view=SettingsPage ssr=SsrMode::Async />
                    <Route path=path!("/dashboard") view=DashboardPage ssr=SsrMode::Async />
                </Routes>
            </Shell>
        </Router>
    }
}

#[component]
fn Icon(
    #[prop(into)] kind: String,
    #[prop(default = 16)] size: u32,
    #[prop(optional, into)] color: Option<String>,
) -> impl IntoView {
    view! {
        <svg class="icon" viewBox="0 0 24 24" width=size height=size fill="none" stroke="currentColor" stroke-width="2"
             stroke-linecap="round" stroke-linejoin="round" style=color.map(|c| format!("color:{c}"))
             inner_html=data::icon(&kind)></svg>
    }
}

#[component]
fn NavItem(
    href: &'static str,
    icon: &'static str,
    label: &'static str,
    #[prop(optional)] prefix: bool,
    #[prop(optional)] children: Option<Children>,
) -> impl IntoView {
    let path = use_location().pathname;
    let class = move || {
        let p = path.get();
        if (prefix && p.starts_with(href)) || p == href { "nav active" } else { "nav" }
    };
    view! { <a class=class href=href><Icon kind=icon /><span class="label">{label}</span>{children.map(|c| c())}</a> }
}

#[component]
fn Section(title: &'static str, #[prop(optional)] open: bool, children: ChildrenFn) -> impl IntoView {
    let open = RwSignal::new(open);
    view! {
        <div class=move || if open.get() { "section open" } else { "section" }>
            <button class="summary" on:click=move |_| open.update(|o| *o = !*o)>
                <svg class="chev" viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><path d="m9 18 6-6-6-6" /></svg>
                <span class="caption">{title}</span>
            </button>
            <Show when=move || open.get()>
                <div class="stack enter">{children()}</div>
            </Show>
        </div>
    }
}

#[component]
fn Shell(children: Children) -> impl IntoView {
    let shell = expect_context::<ShellState>();
    let path = use_location().pathname;
    Effect::new(move |_| {
        path.track();
        shell.drawer.set(false);
    });
    view! {
        <div class=move || if shell.drawer.get() { "app drawer" } else { "app" }>
            <div class="backdrop" on:click=move |_| shell.drawer.set(false)></div>
            <aside class="sidebar">
                <a class="brand" href="/issues"><span class="logo"><Icon kind="zap" size=13 /></span>"Lumen"</a>
                <a class="nav search" href="/issues"><Icon kind="search" size=14 /><span class="label">"Search…"</span><span class="small">"⌘K"</span></a>
                <NavItem href="/inbox" icon="inbox" label="Inbox"><span class="count">"3"</span></NavItem>
                <NavItem href="/issues" icon="issues" label="Issues" prefix=true />
                <NavItem href="/projects" icon="projects" label="Projects" />
                <NavItem href="/dashboard" icon="dashboard" label="Dashboard"><span class="live-dot"></span></NavItem>
                <Section title="YOUR TEAMS" open=true>
                    <a class="nav" href="/projects"><span class="team" style="background:#4ea7fc"></span><span class="label">"Engineering"</span></a>
                    <a class="nav" href="/projects"><span class="team" style="background:#bb87fc"></span><span class="label">"Design"</span></a>
                    <a class="nav" href="/projects"><span class="team" style="background:#f2994a"></span><span class="label">"Product"</span></a>
                </Section>
                <Section title="FAVORITES">
                    <a class="nav" href="/issues/LUM-101"><Icon kind="status-progress" color="#f2c94c" /><span class="label">"Server rendering"</span></a>
                    <a class="nav" href="/issues/LUM-102"><Icon kind="status-review" color="#4cb782" /><span class="label">"Dark mode"</span></a>
                </Section>
                <div class="grow"></div>
                <NavItem href="/settings" icon="settings" label="Settings" />
            </aside>
            <div class="main"><div class="panel">{children()}</div></div>
        </div>
    }
}

#[component]
fn TopBar(title: &'static str, icon: &'static str) -> impl IntoView {
    let shell = expect_context::<ShellState>();
    let menu = RwSignal::new(false);
    view! {
        <div class="topbar">
            <button class="iconbtn hamburger" on:click=move |_| shell.drawer.update(|d| *d = !*d)><Icon kind="menu" size=18 /></button>
            <div class="title"><span class="muted"><Icon kind=icon /></span><span class="trim">{title}</span></div>
            <a class="iconbtn" href="/theme/toggle" rel="external"><Icon kind=if shell.dark { "sun" } else { "moon" } /></a>
            <a class="iconbtn" href="/inbox"><Icon kind="bell" /></a>
            <div class="menu-wrap">
                <button class="iconbtn" on:click=move |_| menu.update(|m| *m = !*m)><span class="avatar" style="background:#5e6ad2">"ED"</span></button>
                <Show when=move || menu.get()>
                    <div class="menu" on:click=move |_| menu.set(false)>
                        <a href="/settings"><Icon kind="user" /><span>"My profile"</span></a>
                        <a href="/theme/toggle" rel="external"><Icon kind="moon" /><span>"Toggle theme"</span></a>
                        <a href="/settings"><Icon kind="settings" /><span>"Settings"</span></a>
                        <hr />
                        <a href="/issues"><Icon kind="log-out" /><span>"Log out"</span></a>
                    </div>
                </Show>
            </div>
        </div>
    }
}

fn label_pills(labels: &[String]) -> impl IntoView + use<> {
    labels.iter().map(|n| {
        let l = data::label(n);
        view! { <span class="pill"><span class="dot" style=format!("background:{}", l.color)></span><span class="small muted">{l.name.as_str()}</span></span> }
    }).collect_view()
}

fn avatar(i: &Issue) -> impl IntoView + use<> {
    let p = i.person();
    view! { <span class="avatar" style=format!("background:{}", p.color)>{p.initials.as_str()}</span> }
}

#[server]
pub async fn all_issues() -> Result<Vec<Issue>, ServerFnError> {
    Ok(data::store::all().to_vec())
}

#[server]
pub async fn get_issue(id: String) -> Result<Option<Issue>, ServerFnError> {
    Ok(data::store::find(&id))
}

#[component]
fn IssuesPage() -> impl IntoView {
    let issues = Resource::new(|| (), |_| all_issues());
    view! {
        <Title text="Issues · Lumen" />
        <TopBar title="Issues" icon="issues" />
        <Suspense>
            {move || Suspend::new(async move {
                let issues = issues.await.unwrap_or_default();
                let active = issues.iter().filter(|i| i.status == "progress" || i.status == "review").count();
                let groups = fix().statuses.iter().filter_map(|s| {
                    let mut g: Vec<&Issue> = issues.iter().filter(|i| i.status == s.key).collect();
                    g.sort_by(|a, b| b.updated.cmp(&a.updated));
                    (!g.is_empty()).then(|| view! {
                        <div>
                            <div class="group"><Icon kind=format!("status-{}", s.key) color=s.color.as_str() />{s.name.as_str()}" "<span class="n">{g.len()}</span></div>
                            {g.into_iter().map(|i| view! {
                                <a class="row" href=i.href()>
                                    <Icon kind=i.priority_icon() color=i.priority_color() size=14 />
                                    <span class="faint hide-480">{i.id.clone()}</span>
                                    <Icon kind=i.status_icon() color=i.status_color() size=14 />
                                    <span class="trim">{i.title.clone()}</span>
                                    <span class="labels hide-820">{label_pills(&i.labels)}</span>
                                    {avatar(i)}
                                    <span class="faint small nowrap hide-640">{i.updated_text.clone()}</span>
                                </a>
                            }).collect_view()}
                        </div>
                    })
                }).collect_view();
                view! {
                    <div class="toolbar">
                        <div class="grow">
                            <span class="pill on small">"All"</span><span class="pill small muted">"Active"</span><span class="pill small muted">"Backlog"</span>
                            <span class="faint small hide-640" style="margin-left:6px">{format!("{} issues · {active} active", issues.len())}</span>
                        </div>
                        <a class="btn" href="/issues/LUM-101"><Icon kind="plus" size=14 />"New issue"</a>
                    </div>
                    {groups}
                }
            })}
        </Suspense>
    }
}

#[derive(Clone, Debug, Default, PartialEq, Serialize, Deserialize)]
pub struct Saved {
    pub error: String,
    pub saved: String,
}

#[server]
pub async fn save_issue(
    id: String,
    title: String,
    description: String,
    status: String,
    priority: String,
    assignee: String,
) -> Result<Saved, ServerFnError> {
    let title = title.trim().to_string();
    if title.is_empty() {
        return Ok(Saved { error: "Title is required.".into(), saved: String::new() });
    }
    data::store::update(&id, |i| {
        i.title = title.clone();
        i.description = description.clone();
        i.status = status.clone();
        i.priority = priority.clone();
        i.assignee = assignee.clone();
    });
    Ok(Saved { error: String::new(), saved: "Changes saved.".into() })
}

#[component]
fn IssuePage() -> impl IntoView {
    let params = use_params_map();
    let save = ServerAction::<SaveIssue>::new();
    let issue = Resource::new(
        move || (params.read().get("id").unwrap_or_default(), save.version().get()),
        |(id, _)| get_issue(id),
    );
    view! {
        <TopBar title="Issue" icon="issues" />
        <Transition>
            {move || Suspend::new(async move {
                match issue.await {
                    Ok(Some(i)) => view! { <IssueForm issue=i save /> }.into_any(),
                    _ => {
                        #[cfg(feature = "ssr")]
                        if let Some(r) = use_context::<leptos_axum::ResponseOptions>() {
                            r.set_status(axum::http::StatusCode::NOT_FOUND);
                        }
                        view! { <Title text="Not found" /><div style="padding:28px">"Issue not found."</div> }.into_any()
                    }
                }
            })}
        </Transition>
    }
}

#[component]
fn IssueForm(issue: Issue, save: ServerAction<SaveIssue>) -> impl IntoView {
    let result = move || save.value().get().and_then(Result::ok).unwrap_or_default();
    let p = issue.person();
    view! {
        <Title text=format!("{} · {}", issue.id, issue.title) />
        <ActionForm action=save attr:class="detail">
            <input type="hidden" name="id" value=issue.id.clone() />
            <div class="left">
                <div style="display:flex;gap:6px;align-items:center"><a class="nav" style="padding:2px 4px" href="/issues">"Issues"</a><span class="faint">"›"</span><span class="muted">{issue.id.clone()}</span></div>
                <input class="title-input" name="title" value=issue.title.clone() placeholder="Issue title" />
                <textarea class="textarea" name="description" placeholder="Add a description…">{issue.description.clone()}</textarea>
                {move || { let e = result().error; (!e.is_empty()).then(|| view! { <div class="error">{e}</div> }) }}
                {move || { let s = result().saved; (!s.is_empty()).then(|| view! { <div class="muted">{s}</div> }) }}
                <div style="display:flex;gap:8px;align-items:center"><button class="btn" type="submit">"Save changes"</button><a class="nav" href="/issues">"Cancel"</a></div>
                <div class="card enter" style="margin-top:10px;display:flex;flex-direction:column;gap:12px">
                    <span class="caption">"ACTIVITY"</span>
                    <div style="display:flex;gap:10px;align-items:center"><span class="avatar" style=format!("background:{}", p.color)>{p.initials.as_str()}</span><span class="muted">"Assigned the issue and moved it to this status."</span></div>
                    <div style="display:flex;gap:10px;align-items:center"><span class="avatar" style="background:#5e6ad2">"ED"</span><span class="muted">"Created the issue from the projects view."</span></div>
                </div>
            </div>
            <div class="right">
                <div class="field"><span class="caption">"STATUS"</span><select class="select" name="status">
                    {fix().statuses.iter().map(|s| view! { <option value=s.key.as_str() selected=s.key == issue.status>{s.name.as_str()}</option> }).collect_view()}
                </select></div>
                <div class="field"><span class="caption">"PRIORITY"</span><select class="select" name="priority">
                    {data::priorities().map(|(k, v)| view! { <option value=k selected=k == issue.priority>{v}</option> }).collect_view()}
                </select></div>
                <div class="field"><span class="caption">"ASSIGNEE"</span><select class="select" name="assignee">
                    {fix().people.iter().map(|p| view! { <option value=p.name.as_str() selected=p.name == issue.assignee>{p.name.as_str()}</option> }).collect_view()}
                </select></div>
                <div class="field"><span class="caption">"LABELS"</span><div class="labels" style="flex-wrap:wrap">{label_pills(&issue.labels)}</div></div>
                <div class="field"><span class="caption">"TEAM"</span><span>{issue.team.clone()}</span></div>
                <div class="field"><span class="caption">"UPDATED"</span><span class="muted">{issue.updated_text.clone()}</span></div>
            </div>
        </ActionForm>
    }
}

#[component]
fn ProjectsPage() -> impl IntoView {
    let issues = Resource::new(|| (), |_| all_issues());
    view! {
        <Title text="Projects · Lumen" />
        <TopBar title="Projects" icon="projects" />
        <div class="toolbar" style="display:block;padding:12px 16px"><div class="h2">"v2 Launch"</div><div class="muted small">"Team board · drag the cards around in your head"</div></div>
        <Suspense>
            {move || Suspend::new(async move {
                let issues = issues.await.unwrap_or_default();
                let columns = ["backlog", "todo", "progress", "review", "done"].into_iter().map(|key| {
                    let s = data::status(key);
                    let mut items: Vec<&Issue> = issues.iter().filter(|i| i.status == key).collect();
                    items.sort_by(|a, b| a.id.cmp(&b.id));
                    view! {
                        <div class="column">
                            <div class="column-head"><Icon kind=format!("status-{key}") color=s.color.as_str() />{s.name.as_str()}" "<span class="faint" style="font-weight:400">{items.len()}</span></div>
                            {items.into_iter().map(|i| view! {
                                <a class="kcard enter" href=i.href()>
                                    <div style="display:flex;justify-content:space-between;align-items:center"><span class="faint small">{i.id.clone()}</span>{avatar(i)}</div>
                                    <div style="font-weight:500">{i.title.clone()}</div>
                                    <div class="labels"><span class="pill"><Icon kind=i.priority_icon() color=i.priority_color() size=12 /></span>{label_pills(&i.labels)}</div>
                                </a>
                            }).collect_view()}
                        </div>
                    }
                }).collect_view();
                view! { <div class="board"><div class="columns">{columns}</div></div> }
            })}
        </Suspense>
    }
}

#[component]
fn InboxPage() -> impl IntoView {
    let issues = Resource::new(|| (), |_| all_issues());
    view! {
        <Title text="Inbox · Lumen" />
        <TopBar title="Inbox" icon="inbox" />
        <Suspense>
            {move || Suspend::new(async move {
                let issues = issues.await.unwrap_or_default();
                let items = &fix().notifications;
                let unread = items.iter().filter(|n| n.unread).count();
                let rows = items.iter().filter_map(|n| {
                    let issue = issues.iter().find(|i| i.id == n.issue)?;
                    let who = data::person(&n.who);
                    Some(view! {
                        <a class="inbox-row enter" href=issue.href()>
                            <span class="unread" style=(!n.unread).then_some("visibility:hidden")></span>
                            <span class="avatar" style=format!("background:{}", who.color)>{who.initials.as_str()}</span>
                            <div style="display:flex;flex-direction:column;gap:2px;min-width:0">
                                <div style="display:flex;gap:5px;min-width:0"><b class="trim">{who.name.as_str()}</b><span class="muted trim">{n.text.as_str()}</span><span class="muted hide-520">{issue.id.clone()}</span></div>
                                <div style="display:flex;gap:6px;align-items:center;min-width:0"><Icon kind=issue.status_icon() color=issue.status_color() size=12 /><span class="muted small trim">{issue.title.clone()}</span></div>
                            </div>
                            <div style="display:flex;gap:8px;align-items:center"><span class="hide-520"><Icon kind=n.icon.as_str() color=n.color.as_str() size=14 /></span><span class="faint small nowrap">{data::ago(&n.at)}</span></div>
                        </a>
                    })
                }).collect_view();
                view! {
                    <div class="toolbar"><span class="h2">"Notifications"</span><span class="count">{format!("{unread} unread")}</span></div>
                    {rows}
                }
            })}
        </Suspense>
    }
}

#[server]
pub async fn save_settings(
    name: String,
    email: String,
    weekly: Option<String>,
    desktop: Option<String>,
) -> Result<Saved, ServerFnError> {
    let _ = (weekly, desktop);
    let error = if !email.contains('@') { "The email is not valid." } else if name.trim().is_empty() { "Name is required." } else { "" };
    Ok(Saved { error: error.into(), saved: if error.is_empty() { "Changes saved.".into() } else { String::new() } })
}

#[component]
fn Preview(bg: &'static str, side: &'static str, row: &'static str, accent: &'static str) -> impl IntoView {
    view! {
        <div style=format!("background:{bg};border-radius:6px;height:84px;padding:8px;display:grid;grid-template-columns:40px 1fr;gap:8px")>
            <div style=format!("background:{side};border-radius:4px")></div>
            <div style="display:flex;flex-direction:column;gap:6px">
                <div style=format!("background:{row};border-radius:4px;height:14px")></div><div style=format!("background:{row};border-radius:4px;height:14px")></div>
                <div style=format!("background:{accent};border-radius:4px;height:14px;width:50px")></div>
            </div>
        </div>
    }
}

#[component]
fn SettingsPage() -> impl IntoView {
    let save = ServerAction::<SaveSettings>::new();
    let result = move || save.value().get().and_then(Result::ok).unwrap_or_default();
    view! {
        <Title text="Settings · Lumen" />
        <TopBar title="Settings" icon="settings" />
        <div class="settings">
            <div><div class="h1">"Settings"</div><div class="muted">"Preferences for your account and Lumen's appearance."</div></div>
            <div class="card enter" style="display:flex;flex-direction:column;gap:14px">
                <div class="h2">"Appearance"</div>
                <div class="theme-cards">
                    <a class="theme-card light" href="/theme/light" rel="external"><Preview bg="#f4f5f8" side="#e5e6eb" row="#fff" accent="#5e6ad2" /><div style="display:flex;gap:8px;align-items:center;font-weight:600"><Icon kind="sun" />"Light"</div></a>
                    <a class="theme-card dark" href="/theme/dark" rel="external"><Preview bg="#0b0c0e" side="#1a1b1f" row="#16171b" accent="#6e79e0" /><div style="display:flex;gap:8px;align-items:center;font-weight:600"><Icon kind="moon" />"Dark"</div></a>
                </div>
            </div>
            <ActionForm action=save attr:class="card enter" attr:style="display:flex;flex-direction:column;gap:14px">
                <div class="h2">"Profile"</div>
                <div class="two"><span class="caption">"NAME"</span><span class="caption">"EMAIL"</span><input class="input" name="name" value="Edsger Dijkstra" /><input class="input" type="email" name="email" value="edsger@lumen.dev" /></div>
                <label class="check"><input type="checkbox" name="weekly" checked />"Weekly email digest"</label>
                <label class="check"><input type="checkbox" name="desktop" />"Desktop alerts"</label>
                {move || { let e = result().error; (!e.is_empty()).then(|| view! { <div class="error">{e}</div> }) }}
                {move || { let s = result().saved; (!s.is_empty()).then(|| view! { <div class="success">{s}</div> }) }}
                <div><button class="btn" type="submit">"Save"</button></div>
            </ActionForm>
        </div>
    }
}

#[server]
pub async fn dashboard() -> Result<Snapshot, ServerFnError> {
    Ok((*crate::feed::live().snapshot.borrow().0).clone())
}

#[cfg(feature = "hydrate")]
fn subscribe(set: RwSignal<Option<Snapshot>>) {
    use wasm_bindgen::{closure::Closure, JsCast};
    let Ok(es) = web_sys::EventSource::new("/dashboard/live") else { return };
    let on = Closure::<dyn FnMut(web_sys::MessageEvent)>::new(move |e: web_sys::MessageEvent| {
        if let Some(Ok(s)) = e.data().as_string().map(|t| serde_json::from_str::<Snapshot>(&t)) {
            set.set(Some(s));
        }
    });
    es.set_onmessage(Some(on.as_ref().unchecked_ref()));
    on.forget();
    let es = send_wrapper::SendWrapper::new(es);
    on_cleanup(move || es.close());
}

#[component]
fn DashboardPage() -> impl IntoView {
    let initial = Resource::new(|| (), |_| dashboard());
    let live = RwSignal::new(None::<Snapshot>);
    #[cfg(feature = "hydrate")]
    Effect::new(move |_| subscribe(live));
    view! {
        <Title text="Dashboard · Lumen" />
        <TopBar title="Dashboard" icon="dashboard" />
        <Suspense>
            {move || Suspend::new(async move {
                let first = initial.await.ok();
                move || live.get().or_else(|| first.clone()).map(|d| view! { <Dashboard d /> })
            })}
        </Suspense>
    }
}

#[component]
fn Dashboard(d: Snapshot) -> impl IntoView {
    view! {
        <div class="dash">
            <div class="header">
                <div><div class="h1">"Dashboard"</div><div class="muted">"Real-time sales"</div></div>
                <div class="status"><span class="pulse"></span><span class="muted">{d.updated}</span></div>
            </div>
            <div class="kpis">
                {d.kpis.into_iter().map(|k| view! {
                    <div class="card enter">
                        <div class="muted">{k.name}</div><div class="kpi">{k.value}</div>
                        <div class="delta">{if k.up { view! { <span class="up">"▲"</span> } } else { view! { <span class="down">"▼"</span> } }}<span class="muted">{k.delta}</span></div>
                    </div>
                }).collect_view()}
            </div>
            <div class="charts">
                <div class="card col enter">
                    <div class="row2"><span class="h2">"Active users"</span><span class="muted">{d.traffic_now}</span></div>
                    <svg viewBox="0 0 600 180" preserveAspectRatio="none" class="chart">
                        <polygon points=d.area fill="var(--accent-soft)" />
                        <polyline points=d.line fill="none" stroke="var(--accent)" stroke-width="2" vector-effect="non-scaling-stroke" stroke-linejoin="round" />
                    </svg>
                </div>
                <div class="card col enter">
                    <span class="h2">"Sales by hour"</span>
                    <div class="bars">
                        {d.bars.into_iter().map(|b| view! { <div class="barcol"><span class="tiny">{b.value}</span><div class="bar" style=format!("height:{}px", b.height)></div><span class="tiny">{b.label}</span></div> }).collect_view()}
                    </div>
                </div>
            </div>
            <div class="card enter">
                <div class="h2" style="margin-bottom:8px">"Recent orders"</div>
                {d.orders.into_iter().map(|o| {
                    let label = match o.status.as_str() { "paid" => "Paid", "pending" => "Pending", _ => "Failed" };
                    view! { <div class="order"><span class="muted">{o.id}</span><b>{o.customer}</b><span>{o.amount}</span><span class=format!("badge {}", o.status)>{label}</span></div> }
                }).collect_view()}
            </div>
        </div>
    }
}
