// Screenshots of a dashboard after it has been live for a few seconds (desktop + mobile).
import { chromium } from "playwright-core";
const [url, out] = process.argv.slice(2);
const browser = await chromium.launch({ executablePath: "/usr/bin/chromium", args: ["--no-sandbox"] });
for (const [name, w, h] of [["desktop", 1280, 1000], ["mobile", 390, 844]]) {
  const page = await browser.newPage({ viewport: { width: w, height: h } });
  await page.goto(url);
  await page.waitForTimeout(3000);
  await page.screenshot({ path: `${out}-${name}.png`, fullPage: true });
  await page.close();
}
await browser.close();
