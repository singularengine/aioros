"use client";
import Link from "next/link";
import { useActionState } from "react";
import Icon from "../Icon";
import { saveIssue } from "./actions";

export default function IssueForm({ issue, statuses, priorities, people }) {
  const [state, action] = useActionState(saveIssue.bind(null, issue.id), { error: "", saved: "" });
  return (
    <form action={action} className="detail">
      <div className="left">
        <div style={{ display: "flex", gap: 6, alignItems: "center" }}><Link className="nav" style={{ padding: "2px 4px" }} href="/issues">Issues</Link><span className="faint">›</span><span className="muted">{issue.id}</span></div>
        <input className="title-input" name="title" defaultValue={issue.title} placeholder="Issue title" />
        <textarea className="textarea" name="description" defaultValue={issue.description} placeholder="Add a description…" />
        {state.error && <div className="error">{state.error}</div>}
        {state.saved && <div className="muted">{state.saved}</div>}
        <div style={{ display: "flex", gap: 8, alignItems: "center" }}><button className="btn" type="submit">Save changes</button><Link className="nav" href="/issues">Cancel</Link></div>
        <div className="card enter" style={{ marginTop: 10, display: "flex", flexDirection: "column", gap: 12 }}>
          <span className="caption">ACTIVITY</span>
          <div style={{ display: "flex", gap: 10, alignItems: "center" }}><span className="avatar" style={{ background: issue.person.color }}>{issue.person.initials}</span><span className="muted">Assigned the issue and moved it to this status.</span></div>
          <div style={{ display: "flex", gap: 10, alignItems: "center" }}><span className="avatar" style={{ background: "#5e6ad2" }}>ED</span><span className="muted">Created the issue from the projects view.</span></div>
        </div>
      </div>
      <div className="right">
        <div className="field"><span className="caption">STATUS</span><select className="select" name="status" defaultValue={issue.status}>{statuses.map(s => <option key={s.key} value={s.key}>{s.name}</option>)}</select></div>
        <div className="field"><span className="caption">PRIORITY</span><select className="select" name="priority" defaultValue={issue.priority}>{Object.entries(priorities).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select></div>
        <div className="field"><span className="caption">ASSIGNEE</span><select className="select" name="assignee" defaultValue={issue.assignee}>{people.map(p => <option key={p.name} value={p.name}>{p.name}</option>)}</select></div>
        <div className="field"><span className="caption">LABELS</span><div className="labels" style={{ flexWrap: "wrap" }}>{issue.labelList.map(l => <span className="pill" key={l.name}><span className="dot" style={{ background: l.color }} /><span className="small muted">{l.name}</span></span>)}</div></div>
        <div className="field"><span className="caption">TEAM</span><span>{issue.team}</span></div>
        <div className="field"><span className="caption">UPDATED</span><span className="muted">{issue.updatedText}</span></div>
      </div>
    </form>
  );
}
