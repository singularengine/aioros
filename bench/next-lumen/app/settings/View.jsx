import Icon from "../Icon";
import TopBar from "../TopBar";
import SettingsForm from "./SettingsForm";


const Preview = ({ bg, side, row, accent }) => (
  <div style={{ background: bg, borderRadius: 6, height: 84, padding: 8, display: "grid", gridTemplateColumns: "40px 1fr", gap: 8 }}>
    <div style={{ background: side, borderRadius: 4 }} />
    <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
      <div style={{ background: row, borderRadius: 4, height: 14 }} /><div style={{ background: row, borderRadius: 4, height: 14 }} />
      <div style={{ background: accent, borderRadius: 4, height: 14, width: 50 }} />
    </div>
  </div>
);

export default function SettingsView() {
  return (
    <>
      <TopBar title="Settings" icon="settings" />
      <div className="settings">
        <div><div className="h1">Settings</div><div className="muted">Preferences for your account and Lumen's appearance.</div></div>
        <div className="card enter" style={{ display: "flex", flexDirection: "column", gap: 14 }}>
          <div className="h2">Appearance</div>
          <div className="theme-cards">
            <a className="theme-card light" href="/theme/light"><Preview bg="#f4f5f8" side="#e5e6eb" row="#fff" accent="#5e6ad2" /><div style={{ display: "flex", gap: 8, alignItems: "center", fontWeight: 600 }}><Icon kind="sun" />Light</div></a>
            <a className="theme-card dark" href="/theme/dark"><Preview bg="#0b0c0e" side="#1a1b1f" row="#16171b" accent="#6e79e0" /><div style={{ display: "flex", gap: 8, alignItems: "center", fontWeight: 600 }}><Icon kind="moon" />Dark</div></a>
          </div>
        </div>
        <SettingsForm />
      </div>
    </>
  );
}
