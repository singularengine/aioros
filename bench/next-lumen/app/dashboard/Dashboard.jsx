"use client";
import { useEffect, useState } from "react";

const labels = { paid: "Paid", pending: "Pending", failed: "Failed" };

// Client Component: prerendered with the initial snapshot, then React re-renders on every SSE message.
export default function Dashboard({ initial }) {
  const [d, setD] = useState(initial);
  useEffect(() => {
    const es = new EventSource("/dashboard/live");
    es.onmessage = e => setD(JSON.parse(e.data));
    return () => es.close();
  }, []);
  return (
    <div className="dash">
      <div className="header">
        <div><div className="h1">Dashboard</div><div className="muted">Real-time sales</div></div>
        <div className="status"><span className="pulse" /><span className="muted">{d.updated}</span></div>
      </div>
      <div className="kpis">
        {d.kpis.map(k => (
          <div className="card enter" key={k.name}>
            <div className="muted">{k.name}</div><div className="kpi">{k.value}</div>
            <div className="delta">{k.up ? <span className="up">▲</span> : <span className="down">▼</span>}<span className="muted">{k.delta}</span></div>
          </div>
        ))}
      </div>
      <div className="charts">
        <div className="card col enter">
          <div className="row2"><span className="h2">Active users</span><span className="muted">{d.trafficNow}</span></div>
          <svg viewBox="0 0 600 180" preserveAspectRatio="none" className="chart">
            <polygon points={d.area} fill="var(--accent-soft)" />
            <polyline points={d.line} fill="none" stroke="var(--accent)" strokeWidth="2" vectorEffect="non-scaling-stroke" strokeLinejoin="round" />
          </svg>
        </div>
        <div className="card col enter">
          <span className="h2">Sales by hour</span>
          <div className="bars">
            {d.bars.map(b => <div className="barcol" key={b.label}><span className="tiny">{b.value}</span><div className="bar" style={{ height: b.height + "px" }} /><span className="tiny">{b.label}</span></div>)}
          </div>
        </div>
      </div>
      <div className="card enter">
        <div className="h2" style={{ marginBottom: 8 }}>Recent orders</div>
        {d.orders.map(o => <div className="order" key={o.id}><span className="muted">{o.id}</span><b>{o.customer}</b><span>{o.amount}</span><span className={"badge " + o.status}>{labels[o.status]}</span></div>)}
      </div>
    </div>
  );
}
