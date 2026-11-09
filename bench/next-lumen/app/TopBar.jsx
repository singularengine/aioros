"use client";
import Link from "next/link";
import { useState } from "react";
import Icon from "./Icon";
import { icons } from "../lib/data";
import { useShell } from "./Shell";

// The cached variant prerenders the light theme's icon; on a full page load in the dark theme this inline script swaps
// it before the first paint, as the inline script in <head> does for data-theme. Client renders get the icon from the
// shell's theme and leave the script inert (text/plain), the pattern of
// https://nextjs.org/docs/app/guides/preventing-flash-before-hydration#extracting-a-reusable-component.
const swap = `{var s=document.currentScript.previousElementSibling.firstChild;if(document.documentElement.dataset.theme==="dark")s.innerHTML=${JSON.stringify(icons.sun)}}`;
const ThemeIcon = () => (
  <script type={typeof window === "undefined" ? "text/javascript" : "text/plain"} suppressHydrationWarning dangerouslySetInnerHTML={{ __html: swap }} />
);

export default function TopBar({ title, icon }) {
  const shell = useShell();
  const [menu, setMenu] = useState(false);
  return (
    <div className="topbar">
      <button className="iconbtn hamburger" onClick={shell.toggleDrawer}><Icon kind="menu" size={18} /></button>
      <div className="title"><span className="muted"><Icon kind={icon} /></span><span className="trim">{title}</span></div>
      <a className="iconbtn" href="/theme/toggle"><Icon kind={shell.theme === "dark" ? "sun" : "moon"} /></a>
      {shell.prerendered && <ThemeIcon />}
      <Link className="iconbtn" href="/inbox"><Icon kind="bell" /></Link>
      <div className="menu-wrap">
        <button className="iconbtn" onClick={() => setMenu(!menu)}><span className="avatar" style={{ background: "#5e6ad2" }}>ED</span></button>
        {menu && (
          <div className="menu" onClick={() => setMenu(false)}>
            <Link href="/settings"><Icon kind="user" /><span>My profile</span></Link>
            <a href="/theme/toggle"><Icon kind="moon" /><span>Toggle theme</span></a>
            <Link href="/settings"><Icon kind="settings" /><span>Settings</span></Link>
            <hr />
            <Link href="/issues"><Icon kind="log-out" /><span>Log out</span></Link>
          </div>
        )}
      </div>
    </div>
  );
}
