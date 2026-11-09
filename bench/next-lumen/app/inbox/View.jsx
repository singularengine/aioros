import Link from "next/link";
import Icon from "../Icon";
import TopBar from "../TopBar";
import { notifications } from "../../lib/data";


export default function InboxView() {
  const items = notifications();
  return (
    <>
      <TopBar title="Inbox" icon="inbox" />
      <div className="toolbar"><span className="h2">Notifications</span><span className="count">{items.filter(n => n.unread).length} unread</span></div>
      {items.map((n, k) => (
        <Link className="inbox-row enter" href={n.issue.href} key={k}>
          <span className="unread" style={n.unread ? undefined : { visibility: "hidden" }} />
          <span className="avatar" style={{ background: n.person.color }}>{n.person.initials}</span>
          <div style={{ display: "flex", flexDirection: "column", gap: 2, minWidth: 0 }}>
            <div style={{ display: "flex", gap: 5, minWidth: 0 }}><b className="trim">{n.person.name}</b><span className="muted trim">{n.text}</span><span className="muted hide-520">{n.issue.id}</span></div>
            <div style={{ display: "flex", gap: 6, alignItems: "center", minWidth: 0 }}><Icon kind={n.issue.statusIcon} color={n.issue.statusColor} size={12} /><span className="muted small trim">{n.issue.title}</span></div>
          </div>
          <div style={{ display: "flex", gap: 8, alignItems: "center" }}><span className="hide-520"><Icon kind={n.icon} color={n.color} size={14} /></span><span className="faint small nowrap">{n.time}</span></div>
        </Link>
      ))}
    </>
  );
}
