// Real-time benchmark with real browsers.
// usage: node rt.mjs <url> <clients> <seconds> <serverRootPid>
// Opens <clients> independent browser contexts on the dashboard and measures, over <seconds> after a warm-up:
//  - delivery: updates applied per second per client, end-to-end latency (server timestamp -> DOM change)
//  - network: bytes received per client per update (SSE chunks or WebSocket frames)
//  - client cost: main-thread task time and JS heap per client
//  - server cost: CPU (all cores, % of one core) and peak RSS of the server process tree
// Also reports first-load metrics from the first client: bytes transferred, JS bytes, FCP, time to first live update.
import { chromium } from "playwright-core";
import { readFileSync, readdirSync } from "node:fs";

const [url, clientsArg, secondsArg, pidArg] = process.argv.slice(2);
const clients = Number(clientsArg), seconds = Number(secondsArg), pid = Number(pidArg);
const WARMUP_MS = 4000;

function tree(root) {
  const out = [], stack = [root];
  while (stack.length) {
    const p = stack.pop(); out.push(p);
    try {
      for (const t of readdirSync(`/proc/${p}/task`))
        for (const c of readFileSync(`/proc/${p}/task/${t}/children`, "utf8").trim().split(/\s+/).filter(Boolean)) stack.push(Number(c));
    } catch { }
  }
  return out;
}
function serverCpuTicks() {
  let ticks = 0;
  for (const p of tree(pid)) {
    try { const f = readFileSync(`/proc/${p}/stat`, "utf8").split(") ")[1].split(" "); ticks += Number(f[11]) + Number(f[12]); } catch { }
  }
  return ticks; // 1 tick = 10 ms (CLK_TCK = 100)
}
function serverRssMb() {
  let kb = 0;
  for (const p of tree(pid)) {
    try { kb += Number(/VmRSS:\s+(\d+)/.exec(readFileSync(`/proc/${p}/status`, "utf8"))[1]); } catch { }
  }
  return kb / 1024;
}

// Counts DOM updates of the "Updated HH:mm:ss.fff" text and their latency from the server timestamp.
const OBSERVER = () => {
  window.__lat = []; window.__first = null;
  const find = () => [...document.querySelectorAll("div,span")].find(e => e.childElementCount === 0 && e.textContent.startsWith("Updated"));
  let el = find(), last = el?.textContent;
  new MutationObserver(() => {
    if (!el || !el.isConnected) el = find();
    if (!el) return;
    const t = el.textContent;
    if (t === last) return;
    last = t;
    const m = t.match(/(\d+):(\d+):(\d+)\.(\d+)/);
    if (!m) return;
    const d = new Date(); d.setHours(+m[1], +m[2], +m[3], +m[4]);
    window.__lat.push(Date.now() - d.getTime());
    window.__first ??= performance.now();
  }).observe(document.body, { subtree: true, characterData: true, childList: true });
};

const browser = await chromium.launch({ executablePath: "/usr/bin/chromium", args: ["--no-sandbox", "--disable-gpu"] });
const sessions = [];
for (let i = 0; i < clients; i++) {
  const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });
  const page = await context.newPage();
  const cdp = await context.newCDPSession(page);
  const s = { page, cdp, bytes: 0, loadBytes: 0, jsBytes: 0, measuring: false };
  await cdp.send("Network.enable");
  await cdp.send("Performance.enable");
  const types = new Map();
  cdp.on("Network.responseReceived", e => types.set(e.requestId, e.type));
  cdp.on("Network.dataReceived", e => { if (s.measuring) s.bytes += e.encodedDataLength || e.dataLength; });
  cdp.on("Network.loadingFinished", e => {
    s.loadBytes += e.encodedDataLength;
    if (types.get(e.requestId) === "Script") s.jsBytes += e.encodedDataLength;
  });
  cdp.on("Network.webSocketFrameReceived", e => {
    if (s.measuring) s.bytes += e.response.opcode === 2 ? Math.floor(e.response.payloadData.length * 3 / 4) : Buffer.byteLength(e.response.payloadData);
  });
  await page.goto(url, { waitUntil: "load" });
  await page.evaluate(OBSERVER);
  sessions.push(s);
}

await new Promise(r => setTimeout(r, WARMUP_MS));
const firstLoad = await sessions[0].page.evaluate(() => ({
  fcp: performance.getEntriesByName("first-contentful-paint")[0]?.startTime ?? null,
  firstLive: window.__first,
}));
const loadBytes = sessions[0].loadBytes, jsBytes = sessions[0].jsBytes;

// Measurement window
const metric = async s => Object.fromEntries((await s.cdp.send("Performance.getMetrics")).metrics.map(m => [m.name, m.value]));
for (const s of sessions) { s.bytes = 0; s.measuring = true; s.m0 = await metric(s); s.n0 = await s.page.evaluate(() => window.__lat.length); }
const cpu0 = serverCpuTicks(), t0 = Date.now();
let peakRss = 0;
const sampler = setInterval(() => { peakRss = Math.max(peakRss, serverRssMb()); }, 200);
await new Promise(r => setTimeout(r, seconds * 1000));
clearInterval(sampler);
const wall = (Date.now() - t0) / 1000, cpu1 = serverCpuTicks();

let updates = 0, lat = [], bytes = 0, taskSec = 0, heap = 0;
for (const s of sessions) {
  s.measuring = false;
  const m1 = await metric(s);
  const all = await s.page.evaluate(() => window.__lat);
  const mine = all.slice(s.n0);
  updates += mine.length; lat.push(...mine); bytes += s.bytes;
  taskSec += m1.TaskDuration - s.m0.TaskDuration;
  heap += m1.JSHeapUsedSize;
}
await browser.close();

lat.sort((a, b) => a - b);
const q = p => lat.length ? lat[Math.min(lat.length - 1, Math.floor(p * lat.length))] : null;
const serverCpuSec = (cpu1 - cpu0) / 100;
console.log(JSON.stringify({
  clients, seconds: Math.round(wall),
  updatesPerSecPerClient: +(updates / clients / wall).toFixed(2),
  latencyP50: q(0.5), latencyP95: q(0.95), latencyP99: q(0.99),
  bytesPerUpdatePerClient: updates ? Math.round(bytes / updates) : null,
  kbPerSecPerClient: +(bytes / clients / wall / 1024).toFixed(1),
  clientCpuMsPerSec: +(taskSec / clients / wall * 1000).toFixed(1),
  clientHeapMb: +(heap / clients / 1048576).toFixed(1),
  serverCpuPct: +(serverCpuSec / wall * 100).toFixed(1),
  serverCpuMsPerUpdate: updates ? +(serverCpuSec * 1000 / (updates / clients) ).toFixed(2) : null,
  serverPeakRssMb: +peakRss.toFixed(1),
  firstLoadKb: +(loadBytes / 1024).toFixed(1), firstLoadJsKb: +(jsBytes / 1024).toFixed(1),
  fcpMs: firstLoad.fcp && Math.round(firstLoad.fcp), firstLiveUpdateMs: firstLoad.firstLive && Math.round(firstLoad.firstLive),
}));
