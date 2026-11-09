"use client";
import { useActionState } from "react";
import { saveSettings } from "./actions";

export default function SettingsForm() {
  const [state, action] = useActionState(saveSettings, { error: "", saved: "" });
  return (
    <form action={action} className="card enter" style={{ display: "flex", flexDirection: "column", gap: 14 }}>
      <div className="h2">Profile</div>
      <div className="two"><span className="caption">NAME</span><span className="caption">EMAIL</span><input className="input" name="name" defaultValue="Edsger Dijkstra" /><input className="input" type="email" name="email" defaultValue="edsger@lumen.dev" /></div>
      <label className="check"><input type="checkbox" name="weekly" defaultChecked />Weekly email digest</label>
      <label className="check"><input type="checkbox" name="desktop" />Desktop alerts</label>
      {state.error && <div className="error">{state.error}</div>}
      {state.saved && <div className="success">{state.saved}</div>}
      <div><button className="btn" type="submit">Save</button></div>
    </form>
  );
}
