import "../open.js";
const parser = new DOMParser();
const pageAddress = () => location.pathname + location.search;
let shownAddress = pageAddress();
let stream = null,
  streamedUrl,
  connection = null;
function morph(current, next) {
  let flagChanged;
  if (current.nodeType !== next.nodeType || current.nodeName !== next.nodeName) {
    const x = next.cloneNode(true);
    current.replaceWith(x);
    initMorphed(x);
    return;
  }
  if (current.nodeType === 3) {
    if (current.data !== next.data) current.data = next.data;
    return;
  }
  if (current.nodeName === "TEMPLATE") {
    current.outerHTML !== next.outerHTML && current.replaceWith(next.cloneNode(true));
    return;
  }
  if (current.nodeType === 1) {
    if (current.tagName === "DIALOG" && current.hasAttribute("data-aioros-modal") && next.hasAttribute("data-aioros-known")) {
      const s = next.hasAttribute("open");
      s && !current.open ? current.showModal() : !s && current.open && current.close();
    }
    if (current.tagName === "DETAILS") {
      const s = next.hasAttribute("open");
      current.so !== void 0 && s !== current.so && (current.open = s);
      current.so = s;
    }
    if (current.hasAttribute("data-aioros-flag")) {
      const s = next.hasAttribute("data-aioros-on");
      current.fo !== void 0 && s !== current.fo && (current.toggleAttribute("data-aioros-on", s), flagChanged = s);
      current.fo = s;
    }
    const k = t => (current.tagName === "DETAILS" || current.tagName === "DIALOG" && current.hasAttribute("data-aioros-modal")) && t.name === "open" || t.name === "data-chosen" || t.name === "data-aioros-revealed" || t.name === "data-aioros-morphing" || t.name === "data-aioros-sized" || t.name === "data-aioros-on" && current.hasAttribute("data-aioros-flag") || t.name === "aria-expanded" && current.hasAttribute("data-aioros-flips") || t.name === "style" && current.hasAttribute("data-aioros-sized"),
      o = morphModule && morphModule.o(current, next);
    for (const t of next.attributes) if (!k(t) && current.getAttribute(t.name) !== t.value) current.setAttribute(t.name, t.value);
    for (const t of [...current.attributes]) if (!k(t) && !next.hasAttribute(t.name)) current.removeAttribute(t.name);
    o && morphModule.v(current, o);
    pointerModule && pointerModule.k(current);
    morphValue(current, next);
  }
  morphChildren(current, next);
  flagChanged !== void 0 && syncFlips(current, flagChanged);
}
function syncFlips(panel, on) {
  panel.querySelectorAll(`button[data-aioros-flips="${CSS.escape(panel.dataset.aiorosFlag)}"]`).forEach(x => x.closest("[data-aioros-flag]") === panel && x.setAttribute("aria-expanded", on));
  const cookieName = panel.dataset.aiorosCookie;
  cookieName && (document.cookie = cookieName + "=" + on + ";path=/;max-age=604800;samesite=lax" + (isSecureContext ? ";secure" : ""));
}
let typedInPost = new Set(),
  changing = null,
  queueRunning = 0,
  leaving = 0;
const pendingFields = [],
  textTypes = /^(text|search|email|url|tel|password|number|date|time|datetime-local|month|week|)$/;
function morphValue(current, next) {
  const tag = current.nodeName,
    field = tag === "OPTION" ? current.closest("select") : current;
  if (field && pendingFields.includes(field.name) || current.dl) return;
  if (tag === "OPTION") {
    const s = next.hasAttribute("selected");
    if (current.selected !== s) current.selected = s;
    return;
  }
  if (tag !== "INPUT" && tag !== "TEXTAREA" || current.type === "file") return;
  if (tag === "TEXTAREA" || textTypes.test(current.type)) {
    if (changing === null || typedInPost.has(current.name) || current === document.activeElement && !changing.includes(current.name)) return;
  }
  if (current.type === "checkbox" || current.type === "radio") {
    const k = next.hasAttribute("checked");
    if (current.checked !== k) current.checked = k;
    return;
  }
  const nextValue = tag === "TEXTAREA" ? next.textContent : next.getAttribute("value") ?? "";
  if (current.value !== nextValue) current.value = nextValue;
}
function morphChildren(current, next) {
  const isDeck = n => n.nodeType === 1 && n.matches(".notice[data-aioros-deck]"),
    currentNodes = [...current.childNodes].filter(n => !isDeck(n)),
    nextNodes = [...next.childNodes].filter(n => !isDeck(n));
  for (let i = 0; i < nextNodes.length; i++) i < currentNodes.length ? morph(currentNodes[i], nextNodes[i]) : initMorphed(current.appendChild(nextNodes[i].cloneNode(true)));
  for (let i = nextNodes.length; i < currentNodes.length; i++) currentNodes[i].remove();
  next.childNodes.forEach(n => isDeck(n) && (swipeModule ? swipeModule.a(current, n) : current.appendChild(n.cloneNode(true))));
}
let morphModule, pointerModule, overlayModule, keysModule, splitModule, carouselModule, filterModule, flagsModule, scrollModule, copyModule, choiceModule, modalModule, rangeModule, scrollerModule, swipeModule, answerModule, draftModule, revealModule;
const styleSelector = "style[data-aioros-style]",
  styleKey = e => e.dataset.aiorosStyle.split(":")[0],
  adoptStyles = n => {
    const c2 = [...document.querySelectorAll(".notice[data-aioros-deck]>.nt")];
    n.querySelectorAll(styleSelector).forEach(e => {
      document.head.querySelectorAll(styleSelector).forEach(o => styleKey(o) === styleKey(e) && (c2.some(x => x._y?.includes(o)) ? o.setAttribute("data-aioros-kept", "") : o.remove()));
      document.head.appendChild(e);
    });
    return n;
  },
  parseFragment = h => adoptStyles(parser.parseFromString("<div>" + h + "</div>", "text/html").body.firstChild),
  initMorphed = x => morphModule && morphModule.i(x),
  importWhenPresent = (s, u, f2) => document.querySelector(s) && import(u).then(x => f2(x.default), () => 0),
  loadModules = () => {
    document.querySelectorAll("details").forEach(d => d.so ??= d.open);
    document.querySelectorAll("[data-aioros-flag]").forEach(p => p.fo ??= p.hasAttribute("data-aioros-on"));
    document.querySelectorAll("[popover]").forEach(p => {
      const v = p.hasAttribute("data-aioros-open");
      if (v !== !!p.ao) {
        p.ao = v;
        try {
          v ? p.showPopover() : p.hidePopover();
        } catch {}
      }
    });
    pointerModule || importWhenPresent("[data-aioros-pointer]", "/_aioros/pointer.js", x => pointerModule = x);
    morphModule || importWhenPresent("[data-aioros-morph]", "/_aioros/morph.js", x => morphModule = x);
    overlayModule || importWhenPresent("[data-aioros-overlay]", "/_aioros/overlay.js", x => overlayModule = x);
    keysModule ? keysModule.i() : importWhenPresent("[data-aioros-tabs],[data-aioros-key],[data-aioros-keys],[data-aioros-calendar]", "/_aioros/keys.js", x => keysModule = x);
    splitModule ? splitModule.i() : importWhenPresent("[data-aioros-split]", "/_aioros/split.js", x => splitModule = x);
    carouselModule ? carouselModule.i() : importWhenPresent("[data-aioros-carousel]", "/_aioros/carousel.js", x => carouselModule = x);
    filterModule ? filterModule.i() : importWhenPresent("[data-aioros-filter]", "/_aioros/filter.js", x => filterModule = x);
    flagsModule || importWhenPresent("[data-aioros-flips]", "/_aioros/flags.js", x => flagsModule = x);
    scrollModule ? scrollModule.i() : importWhenPresent("[data-aioros-scrolls]", "/_aioros/scroll.js", x => scrollModule = x);
    copyModule || importWhenPresent("[data-aioros-copies]", "/_aioros/copy.js", x => copyModule = x);
    choiceModule || importWhenPresent("[data-aioros-chooses]", "/_aioros/choice.js", x => choiceModule = x);
    modalModule ? modalModule.i() : importWhenPresent("[data-aioros-modal]", "/_aioros/modal.js", x => modalModule = x);
    draftModule ? draftModule.i() : importWhenPresent("form :is(input:not([type=hidden],[type=file],[type=submit],[type=button]),textarea,select)", "/_aioros/draft.js", x => draftModule = x);
    rangeModule || importWhenPresent("[data-aioros-range]", "/_aioros/range.js", x => rangeModule = x);
    scrollerModule || importWhenPresent("[data-aioros-scroller]", "/_aioros/scroller.js", x => scrollerModule = x);
    swipeModule ? swipeModule.i() : importWhenPresent("[data-aioros-deck],[data-aioros-swipe]", "/_aioros/swipe.js", x => swipeModule = x);
    answerModule ? answerModule.i() : importWhenPresent("[data-aioros-needs-answer]", "/_aioros/answer.js", x => answerModule = x);
    revealModule ? revealModule.i() : importWhenPresent("[data-aioros-reveal],[data-aioros-count],[data-aioros-spotlight],[data-aioros-parallax]", "/_aioros/reveal.js", x => revealModule = x);
  };
const currentForm = () => document.querySelector("form");
function openLiveStream() {
  stream && stream.close();
  stream = null;
  connection = null;
  delete document.documentElement.dataset.aiorosLive;
  streamedUrl = location.href;
  if (!document.querySelector("meta[name=aioros-live]")) return;
  const p = planFingerprint(),
    t = templates.get(p);
  const q2 = new URLSearchParams(location.search);
  if (t) q2.set("plan", p);
  const qs = q2.toString();
  stream = new EventSource(location.pathname.replace(/\/$/, "") + "/live" + (qs ? "?" + qs : ""));
  stream.addEventListener("connection", e => {
    connection = e.data;
    document.documentElement.dataset.aiorosLive = "";
  });
  stream.onmessage = e => {
    let h = e.data;
    if (h[0] === "{") {
      const j = JSON.parse(h);
      if (!t) return;
      document.title = j.t;
      h = fillTemplate(t, j.d);
      j.s && parseFragment(j.s);
    }
    morphChildren(document.querySelector(".aioros-body"), parseFragment(h));
    setSlotsKey();
    loadModules();
  };
  stream.addEventListener("views", e => {
    setSlotsKey();
    [...adoptStyles(parser.parseFromString(e.data, "text/html")).body.children].forEach(n => {
      const x = document.querySelector('[data-aioros-view="' + CSS.escape(n.getAttribute("data-aioros-view") || "") + '"]');
      if (x) morph(x, n);
    });
  });
}
const templates = new Map(),
  wantedTemplates = new Set(),
  planFingerprint = () => {
    const m2 = document.querySelector("meta[name=aioros-plan]");
    return m2 ? m2.content : "";
  };
function fetchTemplate() {
  const p = planFingerprint();
  if (!p || wantedTemplates.has(p)) return;
  wantedTemplates.add(p);
  fetch("/_aioros/plan/" + p).then(x => x.ok ? x.json() : null).then(t => {
    if (t) {
      templates.set(p, t);
      if (stream && !stream.url.includes("plan=")) openLiveStream();
    }
  }).catch(() => wantedTemplates.delete(p));
}
function navigationHeaders(anew) {
  const headers = {
      "X-Aioros-Navigation": "1"
    },
    fingerprint = planFingerprint(),
    layout = document.querySelector("meta[name=aioros-layout]");
  if (!anew) {
    if (templates.has(fingerprint)) headers["X-Aioros-Plan"] = fingerprint;
    if (layout) headers["X-Aioros-Layout"] = layout.content;
  }
  return headers;
}
function markCurrentLinks() {
  const path = location.pathname.replace(/(.)\/$/, "$1"),
    query = new URLSearchParams(location.search);
  document.querySelectorAll("a[data-nav]").forEach(a => {
    const u = new URL(a.getAttribute("href"), location),
      l = u.pathname.replace(/(.)\/$/, "$1");
    let on = l === "/" || a.hasAttribute("data-exact") ? path === l : path === l || path.startsWith(l + "/");
    for (const [k, v] of u.searchParams) if (!query.getAll(k).includes(v)) on = false;
    on ? a.setAttribute("aria-current", "page") : a.removeAttribute("aria-current");
  });
}
function fillTemplate(template, dynamics) {
  let html = template.s[0];
  for (let i = 0; i < dynamics.length; i++) {
    const k = "" + i,
      v = dynamics[i],
      a = template.c && template.c[k],
      b = template.r && template.r[k];
    html += (a ? v ? fillTemplate(a, v) : "" : b ? v.map(x => fillTemplate(b, x)).join("") : v) + template.s[i + 1];
  }
  return html;
}
const isPageAnswer = ct => ct.includes("text/html") || ct.includes("application/json");
const prefetched = new Map();
const reportFault = r => {
    if (r.fault && window.aiorosFault) aiorosFault(JSON.parse(r.text));
    return r;
  },
  fetchPage = (address, anew) => fetch(address, {
    headers: navigationHeaders(anew)
  }).then(async x => ({
    ok: x.ok,
    ct: x.headers.get("content-type") || "",
    url: x.url,
    part: x.headers.get("X-Aioros-Partial") === "1",
    k: x.headers.get("X-Aioros-Slots"),
    fault: x.headers.has("X-Aioros-Fault"),
    text: await x.text()
  }));
function prefetch(url) {
  if (!prefetched.has(url)) prefetched.set(url, {
    t: Date.now(),
    p: fetchPage(url).catch(() => null)
  });
}
function scrollToTop() {
  scrollTo(0, 0);
  const g = document.querySelector("[data-aioros-page]");
  document.querySelectorAll(".vs,.vsx").forEach(x => (!g || x.contains(g)) && (x.scrollTop = 0));
}
async function go(url, push, keep, z) {
  const hit = prefetched.get(url);
  prefetched.delete(url);
  let r = hit && Date.now() - hit.t < 3e4 ? await hit.p : null;
  if (!r) {
    try {
      r = await fetchPage(url, z);
    } catch {
      location.href = url;
      return;
    }
  }
  if (z) push = r.url !== location.href;
  if (r.fault) {
    reportFault(r);
    return;
  }
  if (!r.ok || !isPageAnswer(r.ct)) {
    location.href = url;
    return;
  }
  await showAnswer(r, push, keep);
}
async function showAnswer(r, push, keep) {
  if (r.ct.includes("application/json")) {
    const j = JSON.parse(r.text),
      t = templates.get(planFingerprint());
    if (!t) {
      location.href = r.url;
      return;
    }
    document.title = j.t;
    setSlotsKey(r.k);
    j.s && parseFragment(j.s);
    morphChildren(document.querySelector(".aioros-body"), parseFragment(fillTemplate(t, j.d)));
    if (push) {
      history.pushState(null, "", r.url);
      shownAddress = pageAddress();
    }
    if (!keep) scrollToTop();
    resetPopovers();
    openPrompts();
    streamedUrl !== location.href && openLiveStream();
    loadModules();
    return;
  }
  const d = parser.parseFromString(r.text, "text/html");
  if (!d.querySelector("form") || !currentForm()) {
    location.href = r.url;
    return;
  }
  document.title = d.title;
  await adoptStylesheets(d);
  document.body.style.cssText = d.body.style.cssText;
  for (const n of ["aioros-live", "aioros-plan", "aioros-layout", "aioros-slots"]) {
    const o = document.querySelector("meta[name=" + n + "]"),
      x = d.querySelector("meta[name=" + n + "]");
    if (o) o.remove();
    if (x) document.head.appendChild(x.cloneNode());
  }
  fetchTemplate();
  const pg = r.part && document.querySelector("[data-aioros-page]");
  if (pg) {
    document.querySelectorAll("dialog.prompt:not([data-aioros-modal]),.notice").forEach(x => x.remove());
    morphChildren(pg, parser.parseFromString("<div><!--p-->" + d.querySelector(".aioros-body").innerHTML + "<!--/p--></div>", "text/html").body.firstChild);
  } else morph(currentForm(), d.querySelector("form"));
  if (push) {
    pendingFields.length = 0;
    history.pushState(null, "", r.url);
    shownAddress = pageAddress();
  }
  if (pg) markCurrentLinks();
  if (!keep) scrollToTop();
  resetPopovers();
  openPrompts();
  r.kept || openLiveStream();
  loadModules();
}
function resetPopovers() {
  document.querySelectorAll("[popover]:not([data-aioros-deck])").forEach(p => {
    if (p.matches(":popover-open")) p.ao = false;
    try {
      p.hidePopover();
    } catch {}
  });
}
function openPrompts() {
  document.querySelectorAll("dialog.prompt[open]").forEach(d => {
    if (!d.matches(":modal")) {
      d.removeAttribute("open");
      d.showModal();
    }
  });
  document.querySelectorAll(".notice[popover]").forEach(p => {
    try {
      p.showPopover();
    } catch {}
  });
}
const isPromptEvent = e => e.target.matches && e.target.matches("dialog.prompt:not([data-aioros-modal])");
const dismissPrompt = v => {
  const form = currentForm();
  if (!form || posting) return;
  const data = new FormData(form);
  data.set("_dismiss", v);
  send(form, data).then(r => r && !failed(r) && applyAnswer(r)).then(loadModules);
};
document.addEventListener("cancel", e => {
  if (isPromptEvent(e)) {
    e.preventDefault();
    dismissPrompt("escape");
  }
}, true);
document.addEventListener("click", e => {
  if (e.target.matches && e.target.matches("dialog.prompt>.pm")) dismissPrompt("outside");
}, true);
document.addEventListener("close", e => {
  if (isPromptEvent(e) && !e.target.open) e.target.remove();
}, true);
document.addEventListener("click", e => {
  const n = e.target.closest && e.target.closest(".notice[popover]:not([data-replay]):not([data-aioros-deck])");
  if (n && e.target.closest("button")) {
    e.preventDefault();
    n.hidePopover();
  }
}, true);
async function adoptStylesheets(d) {
  const q2 = "[data-css]",
    o = [...document.head.querySelectorAll(q2)],
    k = new Set(),
    w = [];
  for (const n of d.head.querySelectorAll(q2)) {
    const e = o.find(x => x.outerHTML === n.outerHTML);
    if (e) {
      k.add(e);
      continue;
    }
    const y = document.importNode(n, true);
    document.head.appendChild(y);
    k.add(y);
    if (y.tagName === "LINK") w.push(new Promise(r => {
      y.onload = y.onerror = r;
    }));
  }
  await Promise.all(w);
  const c2 = [...document.querySelectorAll(".notice[data-aioros-deck]>.nt")];
  o.forEach(x => k.has(x) || (c2.some(y => y._y?.includes(x)) ? x.setAttribute("data-aioros-kept", "") : x.remove()));
}
window.aioros = {
  go
};
const isSamePage = (a, u) => u.pathname === location.pathname && u.search === location.search && (u.hash !== "" || a.getAttribute("href").startsWith("#"));
const sameOriginUrl = a => {
  if (!a || a.target || a.hasAttribute("download")) return null;
  const u = new URL(a.href, location);
  return u.origin === location.origin && !u.pathname.startsWith("/theme/") && u.href !== location.href && !isSamePage(a, u) ? u : null;
};
document.addEventListener("pointerover", e => {
  const u = sameOriginUrl(e.target.closest && e.target.closest("a[href]"));
  if (u) prefetch(u.href);
});
document.addEventListener("click", e => {
  const a = e.target.closest("a[href]");
  if (!a || a.target || a.hasAttribute("download") || e.defaultPrevented || e.button || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
  const u = new URL(a.href, location);
  if (u.origin !== location.origin) return;
  if (isSamePage(a, u)) return;
  e.preventDefault();
  const th = u.pathname.startsWith("/theme/");
  if (th || u.href !== location.href) {
    if (!th && hasUnsavedChanges() && !confirm("Leave this page? Changes you made may not be saved.")) return;
    go(u.href, !th, th, th);
  }
});
const rovingSelector = "[data-aioros-roving]",
  tabbableSelector = "[tabindex]";
const followTouch = e => {
  const t = e.touches[0],
    r = t && e.target.closest && e.target.closest("[data-aioros-follow]"),
    x = r && document.elementFromPoint(t.clientX, t.clientY),
    y = x && x.closest(tabbableSelector);
  if (y && y !== r && r.contains(y)) y.focus({
    preventScroll: 1
  });
};
["touchstart", "touchmove"].forEach(n => document.addEventListener(n, followTouch, {
  passive: 1
}));
const keepsRovingTabs = r => r.dataset.aiorosRovingTabs == "keep",
  rovingItems = r => keepsRovingTabs(r) ? [...r.querySelectorAll("a[href],button,input,select,textarea,[tabindex]")].filter(x => !x.disabled && x.getClientRects().length && !(x.closest("[popover]") && r.contains(x.closest("[popover]")))) : [...r.querySelectorAll(tabbableSelector)];
document.addEventListener("focusin", e => {
  const t = e.target,
    r = t.closest && t.closest(rovingSelector);
  if (r && !keepsRovingTabs(r) && t !== r && t.matches(tabbableSelector)) r.querySelectorAll(tabbableSelector).forEach(x => x.tabIndex = x === t ? 0 : -1);
});
document.addEventListener("keydown", e => {
  const t = e.target,
    r = t.closest && t.closest(rovingSelector);
  if (!r || e.defaultPrevented || e.altKey || e.ctrlKey || e.metaKey) return;
  const k = e.key,
    v = r.dataset.aiorosRoving,
    xs = rovingItems(r),
    i = xs.indexOf(t),
    h = v != "vertical",
    w = v != "horizontal",
    n = r.dataset.aiorosRovingEnds != "off",
    d = {
      Home: n && -1e9,
      End: n && 1e9,
      ArrowLeft: h && -1,
      ArrowRight: +h,
      ArrowUp: w && -1,
      ArrowDown: +w
    }[k];
  if (i < 0 && t !== r) return;
  if (k === "Escape" && r.hasAttribute("data-aioros-roving-escape")) t.blur();
  if (!d) return;
  e.preventDefault();
  xs[Math.max(0, Math.min(i + d, xs.length - 1))]?.focus();
});
let posting = false,
  heldPress = null;
const slotsMeta = () => document.querySelector("meta[name=aioros-slots]");
function setSlotsKey(v) {
  const o = slotsMeta();
  if (!v) {
    if (o) o.remove();
    return;
  }
  if (o) {
    o.content = v;
    return;
  }
  const n = document.createElement("meta");
  n.name = "aioros-slots";
  n.content = v;
  document.head.appendChild(n);
}
async function send(form, data) {
  typedInPost.clear();
  posting = true;
  form.setAttribute("aria-busy", "true");
  let r = null;
  const h = navigationHeaders(),
    k = slotsMeta(),
    y = [...document.head.querySelectorAll(styleSelector)].map(e => e.dataset.aiorosStyle).join(" ");
  if (k) h["X-Aioros-Slots"] = k.content;
  if (y) h["X-Aioros-Styles"] = y;
  if (connection) h["X-Aioros-Live"] = connection;
  try {
    const x = await fetch(location.href, {
      method: "POST",
      body: [...data.values()].some(v => v instanceof File) ? data : new URLSearchParams(data),
      headers: h
    });
    r = {
      ok: x.ok,
      ct: x.headers.get("content-type") || "",
      url: x.url,
      redirected: x.redirected,
      part: x.headers.get("X-Aioros-Partial") === "1",
      to: x.headers.get("X-Aioros-Redirect"),
      v: x.headers.get("X-Aioros-View"),
      s: x.headers.get("X-Aioros-Slot"),
      k: x.headers.get("X-Aioros-Slots"),
      kept: x.headers.get("X-Aioros-Live-Kept") === "1",
      fault: x.headers.has("X-Aioros-Fault"),
      text: await x.text()
    };
    reportFault(r);
  } catch (x) {
    console.error(x);
  }
  posting = false;
  return r;
}
const shapeMarks = form => [...form.querySelectorAll("input[name^=_shape]")].map(x => x.name + "=" + x.value).join("&"),
  releasePress = form => {
  if (!heldPress || posting) return;
  const [n, v, m] = heldPress,
    b = form === currentForm() && shapeMarks(form) === m && [...form.elements].find(x => x.type === "submit" && x.name === n && x.value === v && !x.disabled);
  heldPress = null;
  b && form.requestSubmit(b);
};
const markBusy = form => {
  if (form) posting || queueRunning || pendingFields.length && !retryTimer ? form.setAttribute("aria-busy", "true") : form.removeAttribute("aria-busy");
};
const failed = r => !r || !r.to && (!r.ok || !isPageAnswer(r.ct));
async function applyAnswer(r) {
  if (r.to) {
    leaving = !r.ok;
    location.href = r.to;
    return;
  }
  if (r.s !== null) {
    const o = slotsMeta(),
      n = o ? o.content.split(":")[0] : "",
      xs = [...document.querySelectorAll("[data-aioros-slot]")].filter(e => e.getAttribute("data-aioros-slot") === n),
      x = n && xs[+r.s];
    if (!x) {
      go(location.href, false, true);
      return;
    }
    morphChildren(x, parseFragment("<!--s-->" + r.text + "<!--/s-->"));
    const k = (r.k || "").split(":")[0];
    xs.forEach(e => e.setAttribute("data-aioros-slot", k));
    setSlotsKey(r.k);
    return;
  }
  if (r.v) {
    const ns = [...adoptStyles(parser.parseFromString(r.text, "text/html")).body.children],
      xs = ns.map(n => document.querySelector('[data-aioros-view="' + CSS.escape(n.getAttribute("data-aioros-view") || "") + '"]'));
    if (!ns.length || xs.some(e => !e)) {
      go(location.href, false, true);
      return;
    }
    setSlotsKey();
    ns.forEach((n, i) => morph(xs[i], n));
    return;
  }
  const moved = r.redirected && r.url !== location.href;
  await showAnswer(r, moved, !moved || new URL(r.url).pathname === location.pathname);
}
document.addEventListener("submit", async e => {
  const form = e.target,
    s = e.submitter;
  if (form !== currentForm() || e.defaultPrevented || s && s.getAttribute("formmethod") === "get") return;
  if (posting) {
    e.preventDefault();
    if (queueRunning && s && s.name && !heldPress) heldPress = [s.name, s.value, shapeMarks(form)];
    return;
  }
  e.preventDefault();
  const data = new FormData(form, s),
    nr = s && s.closest(".notice [data-replay],.notice[data-replay]");
  if (nr) data.set("_prompt", nr.dataset.replay);
  if (s && s.name[0] === "f" && s.hasAttribute("data-change")) data.set("_chg", s.name.slice(1));
  const r = await send(form, data);
  if (!r) {
    markSync(form, "unsaved");
    markBusy(form);
    return;
  }
  if (r.fault) {
    markBusy(form);
    return;
  }
  changing = [s && s.name];
  if (failed(r)) {
    changing = null;
    if (s && s.name) {
      const h = document.createElement("input");
      h.type = "hidden";
      h.name = s.name;
      h.value = s.value;
      form.append(h);
    }
    HTMLFormElement.prototype.submit.call(form);
    return;
  }
  await applyAnswer(r);
  changing = null;
  loadModules();
  postPendingFields();
  markBusy(form);
});
document.addEventListener("input", e => {
  const t = e.target,
    d = t.dataset && t.dataset.delay;
  if (t.type === "range") t.style.setProperty("--v", t.value);
  if (posting && t.name) typedInPost.add(t.name);
  if (d && t.name) {
    clearTimeout(t.dl);
    t.dl = setTimeout(() => {
      t.dl = 0;
      pendingFields.includes(t.name) || pendingFields.push(t.name);
      posting || queueRunning || postPendingFields();
    }, +d);
  }
});
const slotsInputOf = l => {
    for (let p = l.parentElement; p; p = p.parentElement) {
      const i = p.querySelector("[data-aioros-slots-input]");
      if (i) return i;
    }
    return null;
  },
  fillSlots = (i, f2) => document.querySelectorAll("[data-aioros-slots]").forEach(l => {
    if (slotsInputOf(l) !== i) return;
    const v = i.value || "",
      bs = [...l.querySelectorAll("[data-aioros-slot-box]")],
      a = f2 ? Math.min(v.length, bs.length - 1) : -1;
    l.querySelectorAll("[data-aioros-slot-text]").forEach((t, k) => t.textContent = v[k] || "");
    bs.forEach((b, k) => b.toggleAttribute("data-aioros-active", k === a));
  });
["input", "focusin", "focusout"].forEach(n => document.addEventListener(n, e => {
  const t = e.target;
  t.matches && t.matches("[data-aioros-slots-input]") && fillSlots(t, e.type !== "focusout");
}));
document.addEventListener("change", e => {
  const t = e.target,
    l = t.type === "file" && (t.closest("label") || t.labels && t.labels[0]);
  if (!l) return;
  const c2 = [...t.files].map(x => x.name).join(", "),
    f2 = l.querySelector("[data-aioros-file-name]");
  l.dataset.chosen = c2;
  if (f2 && c2) f2.textContent = c2;
});
document.addEventListener("toggle", e => {
  const d = e.target,
    c2 = d.tagName === "DETAILS" && d.hasAttribute("data-change") && d.querySelector(":scope>input[type=checkbox][hidden]");
  if (c2 && c2.checked !== d.open) {
    c2.checked = d.open;
    c2.dispatchEvent(new Event("change", {
      bubbles: true
    }));
  }
}, true);
document.addEventListener("change", e => {
  const t = e.target,
    form = currentForm(),
    g = t.closest && t.closest("[data-change]");
  if (!g || !form || !form.contains(g) || !t.name || t.dataset.delay) return;
  if (g.tagName === "DETAILS" && g.querySelector(":scope>input[type=checkbox][hidden]") !== t) return;
  pendingFields.includes(t.name) || pendingFields.push(t.name);
  posting || queueRunning || postPendingFields();
});
async function postPendingFields() {
  const form = currentForm();
  queueRunning = 1;
  while (pendingFields.length && !posting && form === currentForm()) {
    const n = pendingFields.shift(),
      data = new FormData(form);
    data.set("_chg", n.slice(1));
    const r = await send(form, data);
    if (!r) {
      pendingFields.includes(n) || pendingFields.unshift(n);
      markSync(form, "offline");
      scheduleRetry();
      break;
    }
    retryDelay = 0;
    changing = [n];
    if (!failed(r)) await applyAnswer(r);
    changing = null;
    loadModules();
  }
  queueRunning = 0;
  if (!pendingFields.length) {
    markSync(currentForm(), null);
    draftModule && draftModule.c();
  }
  releasePress(form);
  markBusy(form);
}
let retryDelay = 0,
  retryTimer = 0;
const markSync = (form, v) => form && (v ? form.setAttribute("data-aioros-sync", v) : form.removeAttribute("data-aioros-sync")),
  scheduleRetry = () => {
    clearTimeout(retryTimer);
    retryTimer = setTimeout(() => {
      retryTimer = 0;
      posting || queueRunning || postPendingFields();
    }, Math.min(3e4, 1e3 * 2 ** retryDelay++));
  };
addEventListener("online", () => {
  if (pendingFields.length) {
    clearTimeout(retryTimer);
    retryDelay = 0;
    posting || queueRunning || postPendingFields();
  }
});
const hasUnsavedChanges = () => {
  const form = currentForm();
  if (!form) return false;
  if (pendingFields.length || form.hasAttribute("data-aioros-sync")) return true;
  const lo = new Set([...form.querySelectorAll("[data-aioros-local]")].map(x => x.name));
  for (const e of form.elements) {
    if (!e.name || e.disabled || lo.has(e.name) || /^(hidden|file|submit|button|reset|image)$/.test(e.type)) continue;
    if (e.type === "checkbox" || e.type === "radio") {
      if (e.checked !== e.defaultChecked) return true;
    } else if (e.tagName === "SELECT") {
      for (const o of e.options) if (o.selected !== o.defaultSelected) return true;
    } else if (e.value !== e.defaultValue) return true;
  }
  return false;
};
addEventListener("beforeunload", e => {
  if (!leaving && hasUnsavedChanges()) {
    e.preventDefault();
    e.returnValue = "unsaved";
  }
});
const dropSelector = "[data-drop]",
  dragSelector = "[data-drag]";
let dragging = null,
  pressed = null,
  swallowClick = false,
  announcer = null;
const itemHolder = b => {
    const xs = [...b.querySelectorAll(dragSelector)].filter(x => x.closest(dropSelector) === b);
    let c2 = xs.length > 1 ? xs[0].parentElement : b;
    while (c2 !== b && !xs.every(x => c2.contains(x))) c2 = c2.parentElement;
    return c2;
  },
  itemRows = h => [...h.children].filter(x => x.matches(dragSelector) || x.querySelector(dragSelector)),
  dropLists = () => [...document.querySelectorAll(dropSelector)],
  itemRect = x => (x.matches(dragSelector) ? x : x.querySelector(dragSelector)).getBoundingClientRect(),
  isBusy = b => posting || b.hasAttribute("aria-busy"),
  itemName = x => x.getAttribute("aria-label") || (x.innerText || "").trim().split("\n")[0],
  announce = t => {
    if (!announcer) {
      announcer = document.createElement("div");
      announcer.setAttribute("aria-live", "assertive");
      announcer.className = "aioros-live";
      document.body.append(announcer);
    }
    announcer.textContent = t;
  };
function liftItem(item) {
  const holder = itemHolder(item.closest(dropSelector));
  let row = item;
  while (row.parentElement !== holder) row = row.parentElement;
  item.setAttribute("data-dragging", "");
  return {
    it: item,
    el: row,
    from: holder,
    next: row.nextSibling
  };
}
function putBack(drag) {
  drag.it.removeAttribute("data-dragging");
  drag.from.insertBefore(drag.el, drag.next && drag.next.parentNode === drag.from ? drag.next : null);
  settleCopy(drag);
}
const reducedMotion = () => matchMedia("(prefers-reduced-motion: reduce)").matches;
function liftCopy(drag, pointerX, pointerY) {
  const box = drag.it.getBoundingClientRect(),
    copy = drag.it.cloneNode(true);
  ["data-dragging", "data-drag", "id", "tabindex"].forEach(a => copy.removeAttribute(a));
  copy.querySelectorAll("[name],[id]").forEach(n => {
    n.removeAttribute("name");
    n.removeAttribute("id");
  });
  copy.setAttribute("data-aioros-drag-overlay", "");
  copy.setAttribute("aria-hidden", "true");
  Object.assign(copy.style, {
    position: "fixed",
    left: box.left + "px",
    top: box.top + "px",
    width: box.width + "px",
    height: box.height + "px",
    margin: "0",
    pointerEvents: "none",
    zIndex: "2147483646",
    boxSizing: "border-box",
    transition: "none",
    boxShadow: "0 12px 28px rgba(0,0,0,.18),0 2px 6px rgba(0,0,0,.12)"
  });
  document.body.append(copy);
  Object.assign(drag, {
    o: copy,
    dx: pointerX - box.left,
    dy: pointerY - box.top,
    ox: box.left,
    oy: box.top
  });
  followPointer(drag, pointerX, pointerY);
}
function followPointer(drag, pointerX, pointerY) {
  if (drag.o) drag.o.style.transform = "translate(" + (pointerX - drag.dx - drag.ox) + "px," + (pointerY - drag.dy - drag.oy) + "px)" + (reducedMotion() ? "" : " rotate(1.5deg) scale(1.02)");
}
function settleCopy(drag) {
  const copy = drag.o;
  if (!copy) return;
  drag.o = null;
  if (reducedMotion()) {
    copy.remove();
    return;
  }
  const box = drag.it.getBoundingClientRect(),
    animation = copy.animate([{
      transform: copy.style.transform
    }, {
      transform: "translate(" + (box.left - drag.ox) + "px," + (box.top - drag.oy) + "px)"
    }], {
      duration: 180,
      easing: "cubic-bezier(.2,0,0,1)"
    });
  animation.onfinish = animation.oncancel = () => copy.remove();
}
function placeDragged(list, index) {
  const holder = itemHolder(list),
    from = dragging.el.parentElement,
    others = itemRows(holder).filter(x => x !== dragging.el);
  if (from === holder && itemRows(holder).indexOf(dragging.el) === index) return;
  const moved = [...new Set([...itemRows(holder), ...itemRows(from)])],
    boxes = new Map(moved.map(x => [x, x.getBoundingClientRect()]));
  others[index] ? others[index].before(dragging.el) : others.length ? others[others.length - 1].after(dragging.el) : holder.append(dragging.el);
  if (!reducedMotion()) moved.forEach(x => {
    const p = boxes.get(x),
      q2 = x.getBoundingClientRect(),
      dx = p.left - q2.left,
      dy2 = p.top - q2.top;
    if (dx || dy2) x.animate([{
      transform: "translate(" + dx + "px," + dy2 + "px)"
    }, {
      transform: "none"
    }], {
      duration: 150,
      easing: "ease-out"
    });
  });
}
function describePosition() {
  const holder = dragging.el.parentElement,
    list = holder.closest(dropSelector),
    rows = itemRows(holder);
  return itemName(dragging.it) + ", position " + (rows.indexOf(dragging.el) + 1) + " of " + rows.length + " in " + (list.getAttribute("aria-label") || "list " + (dropLists().indexOf(list) + 1));
}
async function dropDragged(drag) {
  drag.it.removeAttribute("data-dragging");
  settleCopy(drag);
  const h = drag.el.parentElement,
    b = h.closest(dropSelector),
    form = currentForm();
  if (h === drag.from && drag.el.nextSibling === drag.next) return;
  const bs = [...new Set([drag.from.closest(dropSelector), b])],
    data = new FormData(form);
  bs.forEach(x => x.setAttribute("aria-busy", "true"));
  data.set("_btn", b.getAttribute("data-drop") + "|" + itemRows(h).indexOf(drag.el) + "|" + drag.it.getAttribute("data-drag"));
  const r = await send(form, data);
  bs.forEach(x => x.removeAttribute("aria-busy"));
  if (failed(r)) {
    putBack(drag);
    announce("The move failed. " + itemName(drag.it) + " is back in its place.");
  } else await applyAnswer(r);
  postPendingFields();
  markBusy(form);
}
document.addEventListener("pointerdown", e => {
  swallowClick = false;
  const it = !e.button && e.target.closest && e.target.closest(dragSelector),
    b = it && it.closest(dropSelector);
  if (!b || dragging || isBusy(b) || e.target.closest("button,a,input,select,textarea")) return;
  pressed = {
    it,
    x: e.clientX,
    y: e.clientY,
    id: e.pointerId,
    t: e.pointerType === "touch" ? Date.now() + 200 : 0
  };
});
document.addEventListener("touchmove", e => {
  if (dragging || pressed && pressed.t && Date.now() >= pressed.t) e.preventDefault();
}, {
  passive: false
});
document.addEventListener("pointermove", e => {
  if (pressed && e.pointerId === pressed.id && !dragging) {
    if (Math.hypot(e.clientX - pressed.x, e.clientY - pressed.y) < 5) return;
    if (Date.now() < pressed.t) {
      pressed = null;
      return;
    }
    dragging = liftItem(pressed.it);
    dragging.id = pressed.id;
    pressed = null;
    liftCopy(dragging, e.clientX, e.clientY);
  }
  if (!dragging || e.pointerId !== dragging.id) return;
  e.preventDefault();
  followPointer(dragging, e.clientX, e.clientY);
  const t = document.elementFromPoint(e.clientX, e.clientY),
    b = t && t.closest(dropSelector);
  if (!b || isBusy(b)) return;
  const s = getComputedStyle(itemHolder(b)),
    row = s.display.includes("flex") && s.flexDirection.startsWith("row"),
    xs = itemRows(itemHolder(b)).filter(x => x !== dragging.el),
    i = xs.findIndex(x => {
      const q2 = itemRect(x);
      return row ? e.clientX < q2.left + q2.width / 2 : e.clientY < q2.top + q2.height / 2;
    });
  placeDragged(b, i < 0 ? xs.length : i);
});
document.addEventListener("pointerup", e => {
  pressed = null;
  if (!dragging || e.pointerId !== dragging.id) return;
  const d = dragging;
  dragging = null;
  swallowClick = true;
  dropDragged(d);
});
document.addEventListener("pointercancel", () => {
  pressed = null;
  if (dragging && dragging.id !== void 0) {
    putBack(dragging);
    dragging = null;
  }
});
document.addEventListener("click", e => {
  if (swallowClick) {
    swallowClick = false;
    e.preventDefault();
    e.stopPropagation();
  }
}, true);
document.addEventListener("keydown", e => {
  const it = e.target,
    k = e.key;
  if (!it.matches || !it.matches(dragSelector) || !it.closest(dropSelector)) return;
  if (!dragging) {
    if ((k === " " || k === "Enter") && !isBusy(it.closest(dropSelector))) {
      e.preventDefault();
      dragging = liftItem(it);
      announce("Picked up " + itemName(it) + ". Use the arrow keys to move, Space to drop, Escape to cancel.");
    }
    return;
  }
  if (dragging.it !== it || dragging.id !== void 0) return;
  e.preventDefault();
  if (k === " " || k === "Enter") {
    const d = dragging,
      w = describePosition();
    dragging = null;
    announce("Dropped " + w + ".");
    dropDragged(d);
    return;
  }
  if (k === "Escape") {
    putBack(dragging);
    dragging = null;
    it.focus();
    announce("Movement cancelled. " + itemName(it) + " is back in its place.");
    return;
  }
  const o = dragging.el.parentElement,
    b = o.closest(dropSelector),
    i = itemRows(o).indexOf(dragging.el),
    ls = dropLists(),
    h = k === "ArrowLeft" ? -1 : k === "ArrowRight" ? 1 : 0,
    t = h && ls[ls.indexOf(b) + h];
  if (k === "ArrowUp") placeDragged(b, Math.max(0, i - 1));else if (k === "ArrowDown") placeDragged(b, i + 1);else if (t && !isBusy(t)) placeDragged(t, i);else return;
  it.focus();
  announce(describePosition());
});
const darkScheme = matchMedia("(prefers-color-scheme: dark)");
function syncSystemTheme() {
  const v = darkScheme.matches ? "dark" : "light",
    k = document.cookie;
  if (!k.includes("theme-system=" + v)) document.cookie = "theme-system=" + v + ";path=/;max-age=31536000;samesite=lax" + (isSecureContext ? ";secure" : "");
  if (/(^|; )theme=system/.test(k) && getComputedStyle(document.documentElement).colorScheme !== v) go(location.href, false, true, 1);
}
darkScheme.addEventListener("change", syncSystemTheme);
syncSystemTheme();
addEventListener("popstate", () => {
  if (pageAddress() === shownAddress) return;
  shownAddress = pageAddress();
  pendingFields.length = 0;
  go(location.href, false);
});
addEventListener("pagehide", () => {
  stream && stream.close();
  stream = null;
  streamedUrl = null;
});
addEventListener("pageshow", e => {
  e.persisted && openLiveStream();
});
openPrompts();
openLiveStream();
loadModules();
(window.requestIdleCallback || setTimeout)(fetchTemplate);
