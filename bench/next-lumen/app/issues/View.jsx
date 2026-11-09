import Link from "next/link";
import Icon from "../Icon";
import TopBar from "../TopBar";
import { allIssues, statuses } from "../../lib/data";


export default function IssuesView() {
  const issues = allIssues();
  const groups = Object.values(statuses)
    .map(s => ({ ...s, issues: issues.filter(i => i.status === s.key).sort((a, b) => b.updated.localeCompare(a.updated)) }))
    .filter(g => g.issues.length);
  const active = issues.filter(i => i.status === "progress" || i.status === "review").length;
  return (
    <>
      <TopBar title="Issues" icon="issues" />
      <div className="toolbar">
        <div className="grow">
          <span className="pill on small">All</span><span className="pill small muted">Active</span><span className="pill small muted">Backlog</span>
          <span className="faint small hide-640" style={{ marginLeft: 6 }}>{issues.length} issues · {active} active</span>
        </div>
        <Link className="btn" href="/issues/LUM-101"><Icon kind="plus" size={14} />New issue</Link>
      </div>
      {groups.map(g => (
        <div key={g.key}>
          <div className="group"><Icon kind={"status-" + g.key} color={g.color} />{g.name} <span className="n">{g.issues.length}</span></div>
          {g.issues.map(i => (
            <Link className="row" href={i.href} key={i.id}>
              <Icon kind={i.priorityIcon} color={i.priorityColor} size={14} />
              <span className="faint hide-480">{i.id}</span>
              <Icon kind={i.statusIcon} color={i.statusColor} size={14} />
              <span className="trim">{i.title}</span>
              <span className="labels hide-820">{i.labelList.map(l => <span className="pill" key={l.name}><span className="dot" style={{ background: l.color }} /><span className="small muted">{l.name}</span></span>)}</span>
              <span className="avatar" style={{ background: i.person.color }}>{i.person.initials}</span>
              <span className="faint small nowrap hide-640">{i.updatedText}</span>
            </Link>
          ))}
        </div>
      ))}
    </>
  );
}
