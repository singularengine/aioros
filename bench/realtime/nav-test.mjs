import { chromium } from "playwright-core";
const [base, flag] = process.argv.slice(2);
const args = ["--no-sandbox", "--disable-gpu"]; if (flag) args.push("--disable-features=ViewTransitionOnNavigation");
const b = await chromium.launch({ executablePath: "/usr/bin/chromium", args });
const ctx = await b.newContext({ viewport: { width: 1366, height: 860 } });
if (process.env.NOVT) await ctx.addInitScript(() => { document.startViewTransition = undefined; });
const p = await ctx.newPage();
await p.goto(base + "/issues"); await p.waitForTimeout(1200);
const t = [];
for (let k = 0; k < 6; k++) for (const [href, text] of [["/projects", "v2 Launch"], ["/issues", "In review"]]) {
  const t0 = Date.now(); await p.locator(`aside a[href="${href}"]`).first().click();
  await p.waitForFunction(([h, x]) => location.pathname === h && document.body.innerText.includes(x), [href, text], { polling: 5 });
  t.push(Date.now() - t0); await p.waitForTimeout(300);
}
t.sort((a, b) => a - b); console.log(flag ? "without view transitions:" : "with view transitions:", "median", t[t.length >> 1], "ms", JSON.stringify(t));
await b.close();
