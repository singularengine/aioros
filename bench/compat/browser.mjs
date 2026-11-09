// The browser side of the compatibility bench, with no dependencies: Chromium driven over the DevTools protocol with
// Node's built-in WebSocket. For each variant it sets the theme cookie, loads the route at the bench's viewport, waits
// for the fonts, turns transitions off, finishes enter animations (endless ones stop at their start) and again those the
// forced state starts (a tip's entrance after its delay), hides tooltips (Avalonia's capture opens none, so a forced
// hover would compare every open tip with the page under it), forces the
// state's pseudo-classes on every element (CSS.forcePseudoState, the counterpart of the pseudo-classes Avalonia's
// capture sets on every input element; focus skips disabled elements, which a browser never focuses), then writes <name>.web.png and <name>.web.json: every visible text with the union of its client rects, grouped by the
// nearest block-level element and cut at its content box when it clips (a trimmed text), plus the text shown by inputs and selects.
//
// usage: node browser.mjs <base-url> <out-dir> <width> <height> <variants.json>
//   variants.json: [{ "name": "...", "route": "/...", "theme": "light|dark", "state": "normal|pointerover|pressed|focus" }]
// Env: CHROMIUM (default /usr/bin/chromium); COMPAT_VARIANT_TIMEOUT, the seconds a variant may take before it is
// captured again in a new tab, once (default 60), so a capture that hangs fails with its variant's name.
import { spawn } from "node:child_process";
import { mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";

const [base, out, width, height, variantsFile] = process.argv.slice(2);
const variants = JSON.parse(await readFile(variantsFile, "utf8"));
const FORCED = { normal: [], pointerover: ["hover"], pressed: ["hover", "active"], focus: ["focus", "focus-visible"] };

const profile = await mkdtemp(join(tmpdir(), "compat-"));
const chromium = spawn(process.env.CHROMIUM ?? "/usr/bin/chromium", [
  "--headless=new", "--no-sandbox", "--disable-gpu", "--hide-scrollbars", "--remote-debugging-port=0",
  "--disable-dev-shm-usage", "--disable-background-timer-throttling", "--disable-renderer-backgrounding",
  "--disable-backgrounding-occluded-windows",
  "--force-device-scale-factor=1", "--font-render-hinting=none", `--user-data-dir=${profile}`, "about:blank",
], { stdio: ["ignore", "ignore", "pipe"] });
const endpoint = await new Promise((resolve, reject) => {
  let log = "";
  chromium.stderr.on("data", chunk => {
    log += chunk;
    const found = /DevTools listening on (ws:\/\/\S+)/.exec(log);
    if (found) resolve(found[1]);
  });
  chromium.on("exit", code => reject(new Error(`Chromium exited (${code}): ${log}`)));
});

const socket = new WebSocket(endpoint);
await new Promise((resolve, reject) => { socket.onopen = resolve; socket.onerror = reject; });
let next = 0;
const pending = new Map();
const listeners = new Set();
socket.onmessage = ({ data }) => {
  const message = JSON.parse(data);
  if (message.id && pending.has(message.id)) {
    const { resolve, reject } = pending.get(message.id);
    pending.delete(message.id);
    message.error ? reject(new Error(`${message.error.message} ${message.error.data ?? ""}`)) : resolve(message.result);
  } else if (message.method) {
    for (const listener of listeners) listener(message);
  }
};
const send = (method, params = {}, sessionId) => new Promise((resolve, reject) => {
  const id = ++next;
  pending.set(id, { resolve, reject });
  socket.send(JSON.stringify({ id, method, params, sessionId }));
});
const once = (method, sessionId) => new Promise(resolve => {
  const listener = message => {
    if (message.method === method && message.sessionId === sessionId) { listeners.delete(listener); resolve(message.params); }
  };
  listeners.add(listener);
});

let targetId, sessionId;
const page = (method, params) => send(method, params, sessionId);
const open = async () => {
  if (targetId) await send("Target.closeTarget", { targetId }).catch(() => {});
  ({ targetId } = await send("Target.createTarget", { url: "about:blank" }));
  ({ sessionId } = await send("Target.attachToTarget", { targetId, flatten: true }));
  await page("Page.enable");
  await page("Page.bringToFront");
  await page("Emulation.setFocusEmulationEnabled", { enabled: true });
  await page("DOM.enable");
  await page("CSS.enable");
  await page("Network.enable");
  await page("Emulation.setDeviceMetricsOverride", { width: +width, height: +height, deviceScaleFactor: 1, mobile: false });
};
await open();
const LIMIT = 1000 * +(process.env.COMPAT_VARIANT_TIMEOUT ?? 60);
const within = (work, name) => {
  let timer;
  const expired = new Promise((_, reject) => { timer = setTimeout(() => reject(new Error(`${name} took more than ${LIMIT / 1000} s`)), LIMIT); });
  return Promise.race([work, expired]).finally(() => clearTimeout(timer));
};

const TEXTS = `(() => {
  const inline = element => { const display = getComputedStyle(element).display; return display === "inline" || display === "contents"; };
  const shown = element => element.checkVisibility({ opacityProperty: true, visibilityProperty: true });
  const blocks = new Map();
  const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
  for (let node; (node = walker.nextNode());) {
    const parent = node.parentElement;
    if (!node.data.trim() || !parent || parent.closest("script,style,noscript,template,select,textarea,option") || !shown(parent)) continue;
    const range = document.createRange();
    range.selectNodeContents(node);
    const rects = [...range.getClientRects()].filter(rect => rect.width > 0 && rect.height > 0);
    if (!rects.length) continue;
    let block = parent;
    while (block.parentElement && block !== document.body && inline(block)) block = block.parentElement;
    const entry = blocks.get(block) ?? { text: "", rects: [], clip: getComputedStyle(block).overflowX === "visible" ? Infinity : block.getBoundingClientRect().right - parseFloat(getComputedStyle(block).paddingRight) - parseFloat(getComputedStyle(block).borderRightWidth) };
    blocks.set(block, entry);
    entry.text += node.data;
    entry.rects.push(...rects);
  }
  const fields = [...document.querySelectorAll("input:not([type=hidden]):not([type=checkbox]):not([type=radio]):not([type=range]),textarea,select")].filter(shown).map(field => {
    const text = field.tagName === "SELECT" ? field.selectedOptions[0]?.textContent ?? "" : field.value || field.placeholder || "";
    const style = getComputedStyle(field);
    const box = field.getBoundingClientRect();
    const context = document.createElement("canvas").getContext("2d");
    context.font = style.font;
    const size = parseFloat(style.fontSize);
    const left = box.left + parseFloat(style.borderLeftWidth) + parseFloat(style.paddingLeft);
    const top = box.top + parseFloat(style.borderTopWidth) + parseFloat(style.paddingTop);
    const inner = box.height - parseFloat(style.borderTopWidth) - parseFloat(style.borderBottomWidth) - parseFloat(style.paddingTop) - parseFloat(style.paddingBottom);
    const lineHeight = style.lineHeight.endsWith("px") ? parseFloat(style.lineHeight) : size * 1.2102;
    const lines = field.tagName === "TEXTAREA" ? text.split("\\n") : [text];
    const first = field.tagName === "TEXTAREA" ? top : top + (inner - lineHeight) / 2;
    return { text, rects: [{ left, top: first, right: left + Math.max(...lines.map(line => context.measureText(line).width)), bottom: first + lines.length * lineHeight }] };
  }).filter(field => field.text.trim());
  return [...blocks.values(), ...fields].map(({ text, rects, clip = Infinity }) => {
    const left = Math.min(...rects.map(rect => rect.left)), top = Math.min(...rects.map(rect => rect.top));
    const right = Math.min(clip, Math.max(...rects.map(rect => rect.right))), bottom = Math.max(...rects.map(rect => rect.bottom));
    return { text, x: left, y: top, width: right - left, height: bottom - top };
  });
})()`;

const STILL = `(() => {
  const style = document.createElement("style");
  style.textContent = "*,*::before,*::after{transition:none!important;caret-color:transparent!important}[role=tooltip]{display:none!important}";
  document.head.append(style);
  for (const animation of document.getAnimations()) {
    if (animation.effect?.getComputedTiming().endTime === Infinity) { animation.pause(); animation.currentTime = 0; } else animation.finish();
  }
  return document.fonts.ready.then(() => document.fonts.status);
})()`;

const FINISHED = `(() => {
  for (const animation of document.getAnimations()) {
    if (animation.effect?.getComputedTiming().endTime === Infinity) { animation.pause(); animation.currentTime = 0; } else animation.finish();
  }
  return new Promise(done => requestAnimationFrame(() => requestAnimationFrame(done)));
})()`;

const capture = async variant => {
  await page("Network.clearBrowserCookies");
  await page("Network.setCookie", { name: "theme", value: variant.theme, url: base, path: "/" });
  const loaded = once("Page.loadEventFired", sessionId);
  await page("Page.navigate", { url: base + variant.route });
  await loaded;
  await page("Runtime.evaluate", { expression: STILL, awaitPromise: true });
  const pseudo = FORCED[variant.state];
  if (pseudo === undefined) throw new Error(`Unknown state ${variant.state}`);
  if (pseudo.length) {
    const { root } = await page("DOM.getDocument", { depth: -1 });
    const selector = variant.state === "focus" ? "body, body *:not(:disabled)" : "body, body *";
    const { nodeIds } = await page("DOM.querySelectorAll", { nodeId: root.nodeId, selector });
    await Promise.all(nodeIds.map(nodeId => page("CSS.forcePseudoState", { nodeId, forcedPseudoClasses: pseudo })));
  }
  await page("Runtime.evaluate", { expression: "new Promise(done => requestAnimationFrame(() => requestAnimationFrame(done)))", awaitPromise: true });
  await page("Runtime.evaluate", { expression: FINISHED, awaitPromise: true });
  const { data } = await page("Page.captureScreenshot", { format: "png", captureBeyondViewport: false });
  await writeFile(join(out, `${variant.name}.web.png`), Buffer.from(data, "base64"));
  const { result } = await page("Runtime.evaluate", { expression: TEXTS, returnByValue: true });
  await writeFile(join(out, `${variant.name}.web.json`), JSON.stringify(result.value));
};

try {
  for (const variant of variants) {
    const started = Date.now();
    try {
      await within(capture(variant), variant.name);
    } catch (error) {
      process.stdout.write(`web ${variant.name}: ${error.message}, retried in a new tab\n`);
      await within(open(), "a new tab");
      await within(capture(variant), variant.name);
    }
    process.stdout.write(`web ${variant.name} ${Date.now() - started} ms\n`);
  }
} finally {
  socket.close();
  const exited = chromium.exitCode === null && chromium.signalCode === null ? new Promise(resolve => chromium.once("exit", resolve)) : Promise.resolve();
  chromium.kill();
  const forced = setTimeout(() => chromium.kill("SIGKILL"), 5000);
  await exited;
  clearTimeout(forced);
  await rm(profile, { recursive: true, force: true, maxRetries: 10, retryDelay: 200 }).catch(() => {});
}
