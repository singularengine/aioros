// One Chromium screenshot of a running Aioros port, for the Showcase's "Ported apps" pages (bench/ports/captures.sh).
// usage: node ports-shots.mjs <url> <out.png> [width] [height]
import { chromium } from "playwright-core";
const [url, out, width = "1280", height = "800"] = process.argv.slice(2);
const browser = await chromium.launch({ executablePath: "/usr/bin/chromium", args: ["--no-sandbox"] });
const context = await browser.newContext({ viewport: { width: +width, height: +height }, deviceScaleFactor: 1 });
const page = await context.newPage();
await page.goto(url, { waitUntil: "networkidle" });
await page.evaluate(() => document.fonts.ready);
await page.waitForTimeout(500);
await page.screenshot({ path: out });
await browser.close();
