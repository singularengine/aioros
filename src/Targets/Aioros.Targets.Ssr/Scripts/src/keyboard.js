const doc = document,
  tabSelector = "[role=tab]",
  tabsOf = x => x.closest("[data-aioros-tabs]"),
  tabListOf = x => x.closest("[role=tablist]"),
  tabsIn = l => [...l.querySelectorAll(tabSelector)].filter(t => tabListOf(t) == l),
  panelsOf = b => [...b.querySelectorAll("[role=tabpanel]")].filter(p => tabsOf(p) == b);
let tabsCount = 0;
const init = () => doc.querySelectorAll("[data-aioros-tabs]").forEach(b => {
    const l = b.querySelector("[role=tablist]");
    if (!l) return;
    const xs = tabsIn(l),
      qs = panelsOf(b);
    b.id || (b.id = "aioros-tabs-" + ++tabsCount);
    xs.forEach((t, k) => {
      const p = qs[k];
      t.id || (t.id = b.id + "-t" + k);
      if (p) {
        p.id || (p.id = b.id + "-p" + k);
        t.setAttribute("aria-controls", p.id);
        p.setAttribute("aria-labelledby", t.id);
      }
    });
    xs.some(t => t.tabIndex == 0) || xs.find(t => !t.disabled)?.setAttribute("tabindex", "0");
  }),
  selectTab = t => {
    const l = tabListOf(t),
      xs = tabsIn(l),
      k = xs.indexOf(t);
    if (t.getAttribute("aria-selected") == "true") return;
    xs.forEach((x, j) => {
      x.setAttribute("aria-selected", j == k);
      x.tabIndex = j == k ? 0 : -1;
    });
    panelsOf(tabsOf(t)).forEach((p, j) => p.hidden = j != k);
    const h = l.querySelector("input[type=hidden]");
    if (h) {
      t.prepend(h);
      h.value = k;
      h.dispatchEvent(new Event("change", {
        bubbles: true
      }));
    }
  };
doc.addEventListener("click", e => {
  const t = e.target.closest?.(tabSelector);
  t && tabsOf(t) && !t.disabled && selectTab(t);
});
doc.addEventListener("keydown", e => {
  const t = e.target,
    m = e.ctrlKey || e.metaKey;
  if (e.defaultPrevented) return;
  const q = '[data-aioros-key="' + (m ? "ctrl+" : "") + (e.altKey ? "alt+" : "") + (e.shiftKey ? "shift+" : "") + e.code + '"]:not(:disabled)',
    S = "[data-aioros-scope]";
  let b;
  for (let x = t.closest ? t : null; x && !b; x = x.parentElement) for (let y = x.previousElementSibling; y?.matches(S) && !b; y = y.previousElementSibling) y.matches(q) && (b = y);
  b = b || doc.querySelector(q + ":not(" + S + ")");
  if (b && (m || e.altKey || !t.closest?.("input,textarea,select,[contenteditable]"))) {
    e.preventDefault();
    b.matches("dialog") ? b.open ? b.close() : b.showModal() : b.click();
    return;
  }
  if (!t.matches?.(tabSelector) || !tabsOf(t) || m || e.altKey) return;
  const v = tabListOf(t).getAttribute("aria-orientation") == "vertical",
    xs = tabsIn(tabListOf(t)).filter(x => !x.disabled),
    c = xs.indexOf(t),
    z = xs.length,
    j = {
      Home: 0,
      End: z - 1,
      [v ? "ArrowUp" : "ArrowLeft"]: (c - 1 + z) % z,
      [v ? "ArrowDown" : "ArrowRight"]: (c + 1) % z
    }[e.key];
  if (j == null) return;
  e.preventDefault();
  xs[j].focus();
  selectTab(xs[j]);
});
const calendarButton = e => e.target.closest?.("[data-aioros-calendar] td button");
doc.addEventListener("click", e => {
  const b = calendarButton(e);
  if (!b) return;
  const h = b.closest("[data-aioros-calendar]").querySelector("input[type=hidden]"),
    p = h.value.split("|");
  p[3] = e.ctrlKey || e.metaKey ? "c" : "";
  h.value = p.join("|");
}, true);
doc.addEventListener("keydown", e => {
  const b = calendarButton(e);
  if (!b || e.defaultPrevented || e.ctrlKey || e.metaKey || e.altKey) return;
  const xs = [...b.closest("table").querySelectorAll("td button")],
    n2 = b.closest("tr").children.length,
    i2 = xs.indexOf(b),
    o = i2 % n2,
    k = {
      ArrowLeft: -1,
      ArrowRight: 1,
      ArrowUp: -n2,
      ArrowDown: n2,
      Home: -o,
      End: n2 - 1 - o
    }[e.key];
  if (k == null) return;
  e.preventDefault();
  const ok = x => x && !x.disabled && x.getAttribute("aria-hidden") != "true" && !x.closest("tr").hidden;
  let j = i2 + k;
  if (e.key == "Home" || e.key == "End") for (; j != i2 && !ok(xs[j]); j -= Math.sign(k));else for (; xs[j] && !ok(xs[j]); j += k);
  ok(xs[j]) && j != i2 && (xs.forEach(x => x.tabIndex = x == xs[j] ? 0 : -1), xs[j].focus());
});
doc.addEventListener("keydown", e => {
  const t = e.target;
  if (e.key !== "Enter" || e.defaultPrevented || !t.matches?.("[data-aioros-picker] input[data-change]")) return;
  e.preventDefault();
  t.dispatchEvent(new Event("change", {
    bubbles: true
  }));
});
doc.addEventListener("keydown", e => {
  const t = e.target,
    m = e.ctrlKey || e.metaKey,
    s2 = t.closest?.("[data-aioros-keyscope]");
  if (e.defaultPrevented || !s2) return;
  const c = (m ? "ctrl+" : "") + (e.altKey ? "alt+" : "") + (e.shiftKey ? "shift+" : "") + e.code,
    f = t.matches("input:not([type=radio],[type=checkbox],[type=button],[type=submit],[type=reset]),textarea,select,[contenteditable]"),
    ch = t.matches("input[type=radio],input[type=checkbox]"),
    p = !m && !e.altKey,
    b = [...s2.querySelectorAll("[data-aioros-keys]")].find(x => x.closest("[data-aioros-keyscope]") == s2 && !x.disabled && x.checkVisibility() && x.dataset.aiorosKeys.split(" ").includes(c));
  if (!b || p && (e.code == "Enter" ? !ch && !f : f || e.code.startsWith("Arrow") && t.type == "radio")) return;
  e.preventDefault();
  (!p || e.code != "Enter" || (ch ? t.checked : t.value.trim())) && b.click();
});
init();
var module = {
  i: init
};
export { module as default };
