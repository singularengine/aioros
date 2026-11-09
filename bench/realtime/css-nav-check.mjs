const BASE = process.argv[2] ?? "http://127.0.0.1:5091";
import { chromium } from "playwright-core";
const browser = await chromium.launch({ executablePath: "/usr/bin/chromium", args: ["--no-sandbox", "--disable-gpu"] });
const look = () => [...document.querySelectorAll("body *")].map(e => {
  const s = getComputedStyle(e);
  return [e.tagName, s.display, s.color, s.backgroundColor, s.fontSize, Math.round(e.getBoundingClientRect().width), Math.round(e.getBoundingClientRect().height)].join(",");
}).join("\n");
const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
const errors = [];
page.on("pageerror", e => errors.push(e.message));
page.on("console", m => { if (m.type() === "error") errors.push(m.text()); });
const css = [];
page.on("request", r => { if (r.url().includes("/_aioros/css/")) css.push(r.url().split("/").pop()); });
const fresh = await browser.newPage({ viewport: { width: 1280, height: 900 } });
await page.goto(`${BASE}/issues`);
await page.evaluate(() => { window.__marker = 1; });
const results = [];
for (const target of ["/projects", "/settings", "/inbox", "/issues"]) {
  await page.click(`a[href="${target}"]`);
  await page.waitForFunction(t => location.pathname === t, target);
  await page.waitForLoadState("networkidle");
  await page.mouse.move(1279, 1);
  await fresh.goto(`${BASE}${target}`);
  const [a, b] = [await page.evaluate(look), await fresh.evaluate(look)];
  const diff = a.split("\n").map((line, i) => [line, b.split("\n")[i]]).filter(([x, y]) => x !== y).slice(0, 4);
  results.push({ target, same: a === b, diff, sheets: await page.evaluate(() => document.querySelectorAll("[data-css]").length) });
}
console.log(JSON.stringify({ errors, sameDocument: await page.evaluate(() => window.__marker === 1), results, cssRequests: css }));
await browser.close();
