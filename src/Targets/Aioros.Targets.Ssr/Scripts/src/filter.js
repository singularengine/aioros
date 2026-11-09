const doc = document,
  optionSelector = "[role=option]",
  filterSelector = "[data-aioros-filter]",
  gapTest = /[\\\/_+.#"@\[\(\{&]/,
  gapGlobal = /[\\\/_+.#"@\[\(\{&]/g,
  spaceTest = /[\s-]/,
  spaceGlobal = /[\s-]/g;
let optionsCount = 0;
const normalized = x => x.toLowerCase().replace(spaceGlobal, " "),
  rank = (s, a, l, b, i2, j, m) => {
    if (j == a.length) return i2 == s.length ? 1 : 0.99;
    const k = i2 + "," + j;
    if (k in m) return m[k];
    const c = b[j];
    let x = l.indexOf(c, i2),
      h = 0;
    while (x >= 0) {
      let v = rank(s, a, l, b, x + 1, j + 1, m),
        w;
      if (v > h) {
        if (x != i2) {
          const p = s.charAt(x - 1);
          if (gapTest.test(p)) {
            v *= 0.8;
            w = s.slice(i2, x - 1).match(gapGlobal);
            if (w && i2 > 0) v *= 0.999 ** w.length;
          } else if (spaceTest.test(p)) {
            v *= 0.9;
            w = s.slice(i2, x - 1).match(spaceGlobal);
            if (w && i2 > 0) v *= 0.999 ** w.length;
          } else {
            v *= 0.17;
            if (i2 > 0) v *= 0.999 ** (x - i2);
          }
        }
        if (s[x] != a[j]) v *= 0.9999;
      }
      if (v < 0.1 && l.charAt(x - 1) == b.charAt(j + 1) || b.charAt(j + 1) == b[j] && l.charAt(x - 1) != b[j]) {
        const t = rank(s, a, l, b, x + 1, j + 2, m);
        if (t * 0.1 > v) v = t * 0.1;
      }
      if (v > h) h = v;
      x = l.indexOf(c, x + 1);
    }
    return m[k] = h;
  },
  score = (s, a) => rank(s, a, normalized(s), normalized(a), 0, 0, {}),
  listboxOf = e => e.querySelector("[role=listbox]"),
  ancestor = (e, t) => {
    while ((e = e.parentElement) && !t(e));
    return e;
  },
  surfaceOf = i2 => ancestor(i2, listboxOf),
  optionsOf = l => [...l.querySelectorAll(optionSelector)],
  isSelectable = o => !o.hidden && !o.disabled && o.getAttribute("aria-disabled") != "true",
  valueOf = o => {
    if (o.dataset.value != null) return o.dataset.value.trim();
    const k = [...o.querySelectorAll("kbd")].map(x => x.textContent);
    let t = o.textContent;
    k.forEach(x => t = t.replace(x, ""));
    return (t.trim() + k.join("")).trim();
  },
  scrollIntoList = (s, o) => {
    for (let p = o.parentElement; p && p != s.parentElement; p = p.parentElement) if (p.scrollHeight > p.clientHeight && /auto|scroll/.test(getComputedStyle(p).overflowY)) {
      const a = o.getBoundingClientRect(),
        b = p.getBoundingClientRect();
      if (a.top < b.top) p.scrollTop -= b.top - a.top;else if (a.bottom > b.bottom) p.scrollTop += a.bottom - b.bottom;
      return;
    }
  },
  select = (i2, o, v) => {
    optionsOf(listboxOf(surfaceOf(i2))).forEach(x => x.setAttribute("aria-selected", x == o));
    o ? (o.id || (o.id = "aioros-o" + ++optionsCount), i2.setAttribute("aria-activedescendant", o.id), v && scrollIntoList(surfaceOf(i2), o)) : i2.removeAttribute("aria-activedescendant");
  },
  sortChildren = (p, q) => [...p.children].sort((a, b) => (q ? (b.fs || 0) - (a.fs || 0) : 0) || a.fi - b.fi).forEach(c => p.appendChild(c)),
  filter = (i2, y) => {
    const q = i2.value,
      s = surfaceOf(i2);
    if (!s) return;
    const l = listboxOf(s),
      xs = optionsOf(l),
      gs = [...l.querySelectorAll("[role=group]")],
      ps = new Set([...xs, ...gs].map(x => x.parentElement));
    if (xs.some(o => o.fv !== valueOf(o))) {
      ps.forEach(p => [...p.children].forEach((c, k) => c.fi = k));
      xs.forEach(o => o.fv = valueOf(o));
    }
    xs.forEach(o => {
      o.fs = q ? score(valueOf(o), q) : 1;
      o.hidden = !o.fs;
    });
    gs.forEach(g => {
      const v2 = optionsOf(g).filter(o => !o.hidden);
      g.hidden = !!q && !v2.length;
      g.fs = Math.max(0, ...v2.map(o => o.fs));
    });
    l.querySelectorAll("hr,[role=separator]").forEach(x => x.hidden = !!q);
    ps.forEach(p => sortChildren(p, q));
    const v = xs.filter(o => !o.hidden);
    s.querySelectorAll("[data-aioros-empty]").forEach(x => x.hidden = v.length > 0);
    select(i2, optionsOf(l).find(isSelectable), y);
  },
  init = () => doc.querySelectorAll(filterSelector).forEach(x => {
    const l = surfaceOf(x) && listboxOf(surfaceOf(x));
    if (!l) return;
    l.id || (l.id = "aioros-l" + ++optionsCount);
    x.setAttribute("aria-controls", l.id);
    filter(x);
  }),
  gr = (t, o, k) => {
    const g = o?.closest("[role=group]"),
      gs = [...listboxOf(surfaceOf(t)).querySelectorAll("[role=group]")].filter(x => !x.hidden);
    for (let j = gs.indexOf(g) + k; g && j >= 0 && j < gs.length; j += k) {
      const x = optionsOf(gs[j]).find(isSelectable);
      if (x) return x;
    }
  };
doc.addEventListener("input", e => e.target.matches?.(filterSelector) && filter(e.target, 1));
doc.addEventListener("keydown", e => {
  const t = e.target;
  if (!t.matches?.(filterSelector) || e.isComposing) return;
  const k = e.key,
    c = e.ctrlKey,
    xs = optionsOf(listboxOf(surfaceOf(t))).filter(isSelectable),
    o = xs.find(x2 => x2.getAttribute("aria-selected") == "true"),
    j = xs.indexOf(o),
    st = k == "ArrowDown" || c && (k == "n" || k == "j") ? 1 : k == "ArrowUp" || c && (k == "p" || k == "k") ? -1 : 0;
  let x;
  if (st) x = e.metaKey ? xs[st > 0 ? xs.length - 1 : 0] : e.altKey ? gr(t, o, st) ?? xs[j + st] : xs[j + st];else if (k == "Home") x = xs[0];else if (k == "End") x = xs[xs.length - 1];else if (k == "Enter") {
    e.preventDefault();
    o?.click();
    return;
  } else return;
  e.preventDefault();
  x && select(t, x, 1);
}, true);
doc.addEventListener("pointermove", e => {
  const o = e.target.closest?.(optionSelector),
    l = o && o.closest("[role=listbox]"),
    s = l && ancestor(l, x => x.querySelector(filterSelector)),
    t = s && s.querySelector(filterSelector);
  if (t && isSelectable(o) && o.getAttribute("aria-selected") != "true") select(t, o);
});
init();
var module = {
  i: init
};
export { module as default };
