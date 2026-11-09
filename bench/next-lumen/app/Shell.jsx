"use client";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { createContext, useContext, useEffect, useState } from "react";
import Icon from "./Icon";

const ShellContext = createContext(null);
export const useShell = () => useContext(ShellContext);

function NavItem({ href, icon, label, children, prefix }) {
  const path = usePathname();
  const active = prefix ? path.startsWith(href) : path === href;
  return <Link className={"nav" + (active ? " active" : "")} href={href}><Icon kind={icon} /><span className="label">{label}</span>{children}</Link>;
}

function Section({ title, initiallyOpen, children }) {
  const [open, setOpen] = useState(initiallyOpen);
  return (
    <div className={"section" + (open ? " open" : "")}>
      <button className="summary" onClick={() => setOpen(!open)}>
        <svg className="chev" viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2"><path d="m9 18 6-6-6-6" /></svg>
        <span className="caption">{title}</span>
      </button>
      {open && <div className="stack enter">{children}</div>}
    </div>
  );
}

// The theme the cached variant's inline script put on <html> (layout.cache.jsx); "light" while prerendering.
const documentTheme = () => typeof document === "undefined" ? "light" : document.documentElement.dataset.theme === "dark" ? "dark" : "light";

// Client Component: sidebar (with mobile drawer state) and the context the per-page top bars use. The live layout passes
// the theme it read from the cookie; the cached layout passes none, and the theme comes from the document.
export default function Shell({ theme: serverTheme, children }) {
  const [theme] = useState(() => serverTheme ?? documentTheme());
  const [drawer, setDrawer] = useState(false);
  const path = usePathname();
  useEffect(() => setDrawer(false), [path]);
  return (
    <ShellContext.Provider value={{ theme, prerendered: serverTheme === undefined, toggleDrawer: () => setDrawer(d => !d) }}>
      <div className={"app" + (drawer ? " drawer" : "")}>
        <div className="backdrop" onClick={() => setDrawer(false)} />
        <aside className="sidebar">
          <Link className="brand" href="/issues"><span className="logo"><Icon kind="zap" size={13} /></span>Lumen</Link>
          <Link className="nav search" href="/issues"><Icon kind="search" size={14} /><span className="label">Search…</span><span className="small">⌘K</span></Link>
          <NavItem href="/inbox" icon="inbox" label="Inbox"><span className="count">3</span></NavItem>
          <NavItem href="/issues" icon="issues" label="Issues" prefix />
          <NavItem href="/projects" icon="projects" label="Projects" />
          <NavItem href="/dashboard" icon="dashboard" label="Dashboard"><span className="live-dot" /></NavItem>
          <Section title="YOUR TEAMS" initiallyOpen>
            <Link className="nav" href="/projects"><span className="team" style={{ background: "#4ea7fc" }} /><span className="label">Engineering</span></Link>
            <Link className="nav" href="/projects"><span className="team" style={{ background: "#bb87fc" }} /><span className="label">Design</span></Link>
            <Link className="nav" href="/projects"><span className="team" style={{ background: "#f2994a" }} /><span className="label">Product</span></Link>
          </Section>
          <Section title="FAVORITES" initiallyOpen={false}>
            <Link className="nav" href="/issues/LUM-101"><Icon kind="status-progress" color="#f2c94c" /><span className="label">Server rendering</span></Link>
            <Link className="nav" href="/issues/LUM-102"><Icon kind="status-review" color="#4cb782" /><span className="label">Dark mode</span></Link>
          </Section>
          <div className="grow" />
          <NavItem href="/settings" icon="settings" label="Settings" />
        </aside>
        <div className="main"><div className="panel">{children}</div></div>
      </div>
    </ShellContext.Provider>
  );
}
