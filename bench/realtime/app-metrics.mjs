// Browser metrics for the Lumen app (same for every implementation).
// usage: node app-metrics.mjs <baseUrl> [desktop|mobile] [runs]   -> prints one JSON object
// Env: BROWSER_PAGES (space-separated, default every page) limits the first-load pages, and BROWSER_SETTLE_MS
// overrides the wait after each load (1500 ms on desktop, 3000 ms on mobile); the quick profile uses both.
// Before measuring, one load of the first page is made and discarded: the first page a browser loads after it starts,
// above all in a fresh job's first Chromium, is slower for reasons of the browser, not of the page (docs/benchmarks.md,
// "What the user sees").
//  - per page, <runs> cold loads, each in a fresh context: bytes, JS bytes, TTFB, FCP, LCP, CLS, long-task blocking
//    time, JS heap (medians)
//  - navigation (desktop only, the sidebar is a drawer on mobile): sidebar clicks /issues -> /projects -> /issues, time
//    until the new content shows, bytes, and INP
//  - menu: avatar click until the menu is visible, and INP
//  - save: a command that goes to the server. After the menu, in the same session, a click on the LUM-105 row (an in-app
//    navigation, not measured), then "Save changes" on the issue form: INP of that click, and the time from the click (its Event Timing timestamp) until
//    "Changes saved." is in the DOM, seen by a MutationObserver. The save rewrites the issue with its own values, so
//    only its "updated" time changes; it runs last.
// Profiles:
//  - desktop: 1366x860, no throttling;
//  - mobile: Lighthouse's default mobile profile (Moto G Power class): 412x823 at DPR 1.75, 4x CPU slowdown and the
//    "Slow 4G" network applied in DevTools terms (562.5 ms request latency, 1474.56 Kbps down, 675 Kbps up), the values
//    Lighthouse uses for devtools throttling (lighthouse/core/config/constants.js).
// INP is read with the Event Timing API (PerformanceObserver "event" + "first-input"), as the web-vitals library does:
// the longest event duration of each interaction, and the worst interaction of the session (fewer than 50 interactions).
import { chromium } from "playwright-core";

const base = process.argv[2];
const profile = process.argv[3] ?? "desktop";
const runs = +(process.argv[4] ?? 5);
const mobile = profile === "mobile";
const PAGES = process.env.BROWSER_PAGES?.split(/\s+/).filter(Boolean) ?? ["/issues", "/issues/LUM-105", "/projects", "/inbox", "/settings"];
const SETTLE = +(process.env.BROWSER_SETTLE_MS || (mobile ? 3000 : 1500));
const browser = await chromium.launch({ executablePath: "/usr/bin/chromium", args: ["--no-sandbox", "--disable-gpu"] });

async function newPage() {
  const ctx = await browser.newContext(mobile
    ? { viewport: { width: 412, height: 823 }, deviceScaleFactor: 1.75, isMobile: true, hasTouch: true,
        userAgent: "Mozilla/5.0 (Linux; Android 11; moto g power (2022)) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Mobile Safari/537.36" }
    : { viewport: { width: 1366, height: 860 } });
  const page = await ctx.newPage();
  const cdp = await ctx.newCDPSession(page);
  await cdp.send("Network.enable"); await cdp.send("Performance.enable");
  if (mobile) {
    await cdp.send("Emulation.setCPUThrottlingRate", { rate: 4 });
    await cdp.send("Network.emulateNetworkConditions", { offline: false, latency: 562.5,
      downloadThroughput: 1474.56 * 1024 / 8, uploadThroughput: 675 * 1024 / 8 });
  }
  const net = { bytes: 0, js: 0 }, types = new Map();
  cdp.on("Network.responseReceived", e => types.set(e.requestId, e.type));
  cdp.on("Network.loadingFinished", e => { net.bytes += e.encodedDataLength; if (types.get(e.requestId) === "Script") net.js += e.encodedDataLength; });
  cdp.on("Network.dataReceived", e => { if (types.get(e.requestId) === "EventSource") net.bytes += e.encodedDataLength; });
  cdp.on("Network.webSocketFrameReceived", e => { net.bytes += e.response.opcode === 2 ? Math.floor(e.response.payloadData.length * 3 / 4) : Buffer.byteLength(e.response.payloadData); });
  await page.addInitScript(() => {
    window.__lcp = 0; window.__cls = 0; window.__tbt = 0; window.__ix = {};
    new PerformanceObserver(l => { for (const e of l.getEntries()) window.__lcp = e.startTime; }).observe({ type: "largest-contentful-paint", buffered: true });
    new PerformanceObserver(l => { for (const e of l.getEntries()) if (!e.hadRecentInput) window.__cls += e.value; }).observe({ type: "layout-shift", buffered: true });
    new PerformanceObserver(l => { for (const e of l.getEntries()) window.__tbt += Math.max(0, e.duration - 50); }).observe({ type: "longtask", buffered: true });
    const ix = l => { for (const e of l.getEntries()) if (e.interactionId) window.__ix[e.interactionId] = Math.max(window.__ix[e.interactionId] ?? 0, e.duration); };
    new PerformanceObserver(ix).observe({ type: "event", durationThreshold: 16, buffered: true });
    new PerformanceObserver(ix).observe({ type: "first-input", buffered: true });
  });
  return { ctx, page, cdp, net };
}

// The worst interaction so far; Event Timing reports an entry after the next paint, so wait for that frame first.
async function inp(page) {
  await page.evaluate(() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(() => setTimeout(r, 50)))));
  return page.evaluate(() => Math.max(0, ...Object.values(window.__ix)));
}

const median = a => { const s = [...a].sort((x, y) => x - y); return s.length ? s[Math.floor(s.length / 2)] : null; };
const result = { profile, runs, pages: {}, navigation: null, menu: null, save: null };

{
  const { ctx, page } = await newPage();
  await page.goto(base + (PAGES[0] ?? "/issues"), { waitUntil: "load" });
  await ctx.close();
}

for (const path of PAGES) {
  const samples = [];
  for (let k = 0; k < runs; k++) {
    const { ctx, page, cdp, net } = await newPage();
    await page.goto(base + path, { waitUntil: "load" });
    await page.waitForTimeout(SETTLE); // let interactive frameworks hydrate / connect
    const m = await page.evaluate(() => ({
      ttfb: performance.getEntriesByType("navigation")[0].responseStart,
      fcp: performance.getEntriesByName("first-contentful-paint")[0]?.startTime ?? null,
      lcp: window.__lcp, cls: window.__cls, tbt: window.__tbt,
      dcl: performance.getEntriesByType("navigation")[0].domContentLoadedEventEnd,
    }));
    const heap = (await cdp.send("Performance.getMetrics")).metrics.find(x => x.name === "JSHeapUsedSize").value;
    samples.push({ ...m, bytes: net.bytes, js: net.js, heap });
    await ctx.close();
  }
  const pick = k => +median(samples.map(r => r[k])).toFixed(k === "cls" ? 3 : 0);
  result.pages[path] = { kb: +(pick("bytes") / 1024).toFixed(1), jsKb: +(pick("js") / 1024).toFixed(1), ttfb: pick("ttfb"),
    fcp: pick("fcp"), lcp: pick("lcp"), cls: pick("cls"), tbt: pick("tbt"), dcl: pick("dcl"), heapMb: +(pick("heap") / 1048576).toFixed(1) };
}

if (!mobile) { // navigation through the sidebar
  const times = [], bytes = [], inps = [];
  for (let k = 0; k < runs; k++) {
    const { ctx, page, net } = await newPage();
    await page.goto(base + "/issues", { waitUntil: "load" }); await page.waitForTimeout(SETTLE);
    for (let round = 0; round < 2; round++) {
      for (const [href, text] of [["/projects", "v2 Launch"], ["/issues", "In review"]]) {
        const b0 = net.bytes, t0 = Date.now();
        await page.locator(`aside a[href="${href}"]`).first().click();
        await page.waitForFunction(t => document.body.innerText.includes(t), text, { polling: 5 });
        await page.waitForFunction(h => location.pathname === h, href, { polling: 5 });
        times.push(Date.now() - t0); bytes.push(net.bytes - b0);
        await page.waitForTimeout(300);
      }
    }
    inps.push(await inp(page));
    await ctx.close();
  }
  result.navigation = { ms: median(times), kb: +(median(bytes) / 1024).toFixed(1), inp: median(inps) };
}

{ // avatar menu, then save on the issue form in the same session (one page load and settle for both)
  const times = [], inps = [], saves = [], saveInps = [];
  for (let k = 0; k < runs; k++) {
    const { ctx, page } = await newPage();
    await page.goto(base + "/issues", { waitUntil: "load" }); await page.waitForTimeout(SETTLE);
    const btn = page.locator("button[popovertarget^=m], .menu-wrap > button").first();
    for (let round = 0; round < 3; round++) {
      const t0 = Date.now();
      await btn.click();
      await page.getByText("My profile").first().waitFor({ state: "visible" });
      times.push(Date.now() - t0);
      await btn.click(); await page.waitForTimeout(300);
    }
    inps.push(await inp(page));

    await page.locator('a[href="/issues/LUM-105"]').first().click();
    const save = page.getByRole("button", { name: "Save changes" });
    await save.waitFor({ state: "visible" });
    await page.waitForFunction(() => location.pathname === "/issues/LUM-105", null, { polling: 5 });
    await inp(page); await page.waitForTimeout(300);
    await page.evaluate(() => {
      window.__ix = {}; window.__saved = null;
      let t0 = 0;
      document.addEventListener("click", e => { t0 = e.timeStamp; }, { capture: true, once: true });
      const seen = new MutationObserver(() => {
        if (document.body.textContent.includes("Changes saved.")) { window.__saved = performance.now() - t0; seen.disconnect(); }
      });
      seen.observe(document.body, { childList: true, subtree: true, characterData: true });
    });
    await save.click();
    if (await page.waitForFunction(() => window.__saved, null, { polling: 5, timeout: 15000 }).then(() => true, () => false)) {
      saves.push(await page.evaluate(() => window.__saved)); saveInps.push(await inp(page));
    }
    await ctx.close();
  }
  result.menu = { ms: median(times), inp: median(inps) };
  result.save = saves.length ? { ms: Math.round(median(saves)), inp: median(saveInps), samples: saves.length } : null;
}

await browser.close();
console.log(JSON.stringify(result));
