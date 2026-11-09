// Screenshots of the SaaS app: pages x themes x viewports, plus the mobile drawer and the user menu open.
// usage: node app-shots.mjs <baseUrl> <outDir> [pages...]
import { chromium } from "playwright-core";
const [base, out, ...only] = process.argv.slice(2);
const pages = only.length ? only : ["/issues", "/issues/LUM-105", "/projects", "/inbox", "/settings", "/dashboard"];
const browser = await chromium.launch({ executablePath: "/usr/bin/chromium", args: ["--no-sandbox"] });
const host = new URL(base).hostname;
for (const theme of ["light", "dark"]) {
  for (const [vp, w, h] of [["desktop", 1366, 860], ["mobile", 390, 844]]) {
    const ctx = await browser.newContext({ viewport: { width: w, height: h }, deviceScaleFactor: 1 });
    await ctx.addCookies([{ name: "theme", value: theme, domain: host, path: "/" }]);
    const page = await ctx.newPage();
    for (const p of pages) {
      await page.goto(base + p);
      await page.waitForTimeout(700);
      const name = p.replace(/\//g, "_").replace(/^_/, "") || "root";
      await page.screenshot({ path: `${out}/${name}-${theme}-${vp}.png` });
    }
    // Selectors for each implementation: XAML (popover attributes), Blazor/Next (classes).
    const drawerBtn = page.locator("button[popovertarget=nav], button.hamburger").first();
    const menuBtn = page.locator("button[popovertarget^=m], .menu-wrap > button").first();
    await page.goto(base + "/issues"); await page.waitForTimeout(1500); // interactive frameworks need to hydrate/connect
    if (vp === "mobile") {
      await drawerBtn.click(); await page.waitForTimeout(500);
      await page.screenshot({ path: `${out}/drawer-${theme}-${vp}.png` });
      await page.keyboard.press("Escape"); await page.goto(base + "/issues"); await page.waitForTimeout(1500);
    }
    await menuBtn.click(); await page.waitForTimeout(400);
    await page.screenshot({ path: `${out}/menu-${theme}-${vp}.png` });
    await ctx.close();
  }
}
await browser.close();
