// Same data as the XAML and Blazor versions (exported fixtures). Issues are mutable in memory (shared via globalThis).
import fixtures from "./fixtures.json";

export const statuses = Object.fromEntries(fixtures.statuses.map(s => [s.key, s]));
export const priorities = fixtures.priorities;
export const people = Object.fromEntries(fixtures.people.map(p => [p.name, p]));
export const peopleList = fixtures.people;
export const labels = Object.fromEntries(fixtures.labels.map(l => [l.name, l]));
export const icons = fixtures.icons;

const NOW = new Date("2026-10-10T12:00:00").getTime();
export function ago(t) {
  const m = Math.max(0, (NOW - new Date(t).getTime()) / 60000);
  return m < 60 ? `${Math.floor(m)}m ago` : m < 1440 ? `${Math.floor(m / 60)}h ago` : `${Math.floor(m / 1440)}d ago`;
}

function decorate(i) {
  const st = statuses[i.status];
  return {
    ...i, href: "/issues/" + i.id, statusIcon: "status-" + i.status, statusColor: st.color, statusName: st.name,
    priorityIcon: "prio-" + i.priority, priorityColor: i.priority === "urgent" ? "#F2994A" : "#8B8F98",
    person: people[i.assignee], labelList: i.labels.map(n => labels[n]), updatedText: ago(i.updated),
  };
}

const store = (globalThis.__lumen ??= { issues: fixtures.issues.map(i => ({ ...i })) });
export const allIssues = () => store.issues.map(decorate);
export const findIssue = id => { const i = store.issues.find(x => x.id === id); return i && decorate(i); };
export function updateIssue(id, patch) {
  store.issues = store.issues.map(i => (i.id === id ? { ...i, ...patch, updated: new Date().toISOString() } : i));
}
export function notifications() {
  const byId = Object.fromEntries(allIssues().map(i => [i.id, i]));
  return fixtures.notifications.map(n => ({ ...n, person: people[n.who], issue: byId[n.issue], time: ago(n.at) }));
}
