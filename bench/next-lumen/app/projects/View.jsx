import Link from "next/link";
import Icon from "../Icon";
import TopBar from "../TopBar";
import { allIssues, statuses } from "../../lib/data";


export default function ProjectsView() {
  const issues = allIssues();
  return (
    <>
      <TopBar title="Projects" icon="projects" />
      <div className="toolbar" style={{ display: "block", padding: "12px 16px" }}><div className="h2">v2 Launch</div><div className="muted small">Team board · drag the cards around in your head</div></div>
      <div className="board"><div className="columns">
        {["backlog", "todo", "progress", "review", "done"].map(key => {
          const s = statuses[key];
          const items = issues.filter(i => i.status === key).sort((a, b) => a.id.localeCompare(b.id));
          return (
            <div className="column" key={key}>
              <div className="column-head"><Icon kind={"status-" + key} color={s.color} />{s.name} <span className="faint" style={{ fontWeight: 400 }}>{items.length}</span></div>
              {items.map(i => (
                <Link className="kcard enter" href={i.href} key={i.id}>
                  <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}><span className="faint small">{i.id}</span><span className="avatar" style={{ background: i.person.color }}>{i.person.initials}</span></div>
                  <div style={{ fontWeight: 500 }}>{i.title}</div>
                  <div className="labels"><span className="pill"><Icon kind={i.priorityIcon} color={i.priorityColor} size={12} /></span>{i.labelList.map(l => <span className="pill" key={l.name}><span className="dot" style={{ background: l.color }} /><span className="small muted">{l.name}</span></span>)}</div>
                </Link>
              ))}
            </div>
          );
        })}
      </div></div>
    </>
  );
}
