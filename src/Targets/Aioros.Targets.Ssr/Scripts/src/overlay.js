const doc = document,
  byId = i => doc.getElementById(i),
  closest = (x, s) => x?.closest?.(s),
  attribute = (t, n) => t.getAttribute(n),
  menuItems = m => [...m.children].filter(x => x.matches("[role^=menuitem]:not([disabled])")),
  show = p => {
    try {
      p.showPopover();
    } catch {}
  },
  hide = p => {
    try {
      p.hidePopover();
    } catch {}
  },
  targetOf = t => byId(attribute(t, "popovertarget")),
  hoverOf = x => closest(x, "[data-aioros-hover]"),
  parentMenu = m => closest(m.parentElement, "[role=menu]"),
  menuBar = m => closest(m, "[role=menubar]"),
  triggerOf = x => {
    const p = closest(x, "[popover]"),
      s = p?.previousElementSibling;
    return closest(x, "[data-hover-open]") || (s?.dataset.hoverOpen ? s : null);
  },
  openSibling = t => [...t.parentElement.children].find(x => x !== t && x.dataset.hoverOpen && targetOf(x)?.matches(":popover-open")),
  W = (t, o, p) => {
    if (!p || !("switch" in t.dataset)) return;
    const e = [...t.parentElement.children],
      f = e.indexOf(t) > e.indexOf(o),
      q2 = targetOf(o);
    p.dataset.motion = f ? "from-end" : "from-start";
    p.w = 1;
    q2 && (q2.dataset.motion = f ? "to-start" : "to-end");
  };
let typed = "",
  typedTimer,
  hovered,
  grace,
  pointerPath;
const placeAt = (p, x, y) => {
    if (!p) return;
    const s = (n, v) => p.style.setProperty("--aioros-" + n, v + "px");
    s("x", x);
    s("y", y);
    show(p);
    const r = p.getBoundingClientRect();
    r.right > innerWidth && s("x", Math.max(0, x - r.width));
    r.bottom > innerHeight && s("y", Math.max(0, y - r.height));
  },
  hoverShow = (w, o) => {
    if (!w || w.s == o) return;
    w.s = o;
    clearTimeout(w.t);
    const p = byId(w.dataset.aiorosHover);
    w.t = setTimeout(() => o ? show(p) : hide(p), +(o ? w.dataset.open : w.dataset.close));
  },
  barItem = (t, k) => {
    const b = menuBar(t),
      xs = [...b.querySelectorAll(":scope>li>[role=menuitem]")],
      i = xs.indexOf(closest(t, "li").firstElementChild),
      n = xs[(i + k + xs.length) % xs.length];
    n.focus();
    return n;
  },
  skipTo = w => {
    const r = doc.documentElement;
    r.setAttribute("data-aioros-tip-skip", "");
    clearTimeout(r.t);
    r.t = setTimeout(() => r.removeAttribute("data-aioros-tip-skip"), +w.dataset.between);
  };
doc.addEventListener("toggle", e => {
  const m = e.target;
  if (e.newState == "closed") {
    const w = hoverOf(m);
    w && (w.s = 0);
    return;
  }
  if (m.matches?.("[role=menu]") && !grace) menuItems(m)[0]?.focus();
  grace = 0;
}, true);
doc.addEventListener("keydown", e => {
  const t = e.target,
    k = e.key;
  if (e.defaultPrevented || e.ctrlKey || e.metaKey || e.altKey) return;
  const w = closest(t, "[data-aioros-context]");
  if (w && !closest(t, "[popover]") && (k == "ContextMenu" || e.shiftKey && k == "F10")) {
    e.preventDefault();
    const r = t.getBoundingClientRect();
    placeAt(byId(w.dataset.aiorosContext), r.left, r.bottom);
    return;
  }
  if (k == "Escape") doc.querySelectorAll(".tip").forEach(x => (x.matches(":hover") || x.querySelector(":focus-visible")) && (x.dataset.off = ""));
  const m = closest(t, "[role=menu]");
  if (!m || t === m && k != "ArrowDown") {
    const s = attribute(t, "aria-haspopup") && targetOf(t);
    if (s && k == "ArrowDown") {
      e.preventDefault();
      show(s);
    } else if (menuBar(t) && (k == "ArrowLeft" || k == "ArrowRight")) {
      e.preventDefault();
      barItem(t, k == "ArrowLeft" ? -1 : 1);
    }
    return;
  }
  const xs = menuItems(m),
    i = xs.indexOf(t),
    p = parentMenu(m);
  let n = -1;
  if (k == "ArrowDown") n = Math.min(i + 1, xs.length - 1);else if (k == "ArrowUp") n = Math.max(i - 1, 0);else if (k == "Home") n = 0;else if (k == "End") n = xs.length - 1;else if (k == "ArrowRight" && attribute(t, "aria-haspopup")) {
    e.preventDefault();
    show(targetOf(t));
    return;
  } else if (k == "ArrowLeft" && p) {
    e.preventDefault();
    hide(m);
    doc.querySelector('[popovertarget="' + m.id + '"]')?.focus();
    return;
  } else if ((k == "ArrowLeft" || k == "ArrowRight") && menuBar(m)) {
    e.preventDefault();
    hide(m);
    show(targetOf(barItem(m, k == "ArrowLeft" ? -1 : 1)));
    return;
  } else if (k.length == 1 && k != " ") {
    clearTimeout(typedTimer);
    typed += k.toLowerCase();
    typedTimer = setTimeout(() => typed = "", 1e3);
    const o = [...typed].every(c2 => c2 == typed[0]) ? typed[0] : typed,
      r = o.length > 1 ? [...xs.slice(i), ...xs.slice(0, i)] : [...xs.slice(i + 1), ...xs.slice(0, i + 1)];
    r.find(x => x.textContent.trim().toLowerCase().startsWith(o))?.focus();
    return;
  }
  if (n >= 0) {
    e.preventDefault();
    xs[n]?.focus();
  }
});
doc.addEventListener("pointermove", e => {
  const t = closest(e.target, "[role^=menuitem]"),
    m = t && closest(t, "[role=menu]");
  if (e.pointerType == "touch" || !m || t.disabled) return;
  doc.activeElement !== t && t.focus({
    preventScroll: true
  });
  const s = attribute(t, "aria-haspopup") && targetOf(t);
  [...m.children].forEach(x => x !== s && x.matches("[popover]:popover-open") && hide(x));
  clearTimeout(hovered);
  if (s && !s.matches(":popover-open")) hovered = setTimeout(() => {
    grace = 1;
    show(s);
  }, 100);
}, {
  passive: true
});
doc.addEventListener("contextmenu", e => {
  const w = closest(e.target, "[data-aioros-context]"),
    p = closest(e.target, "[popover]");
  if (!w) return;
  e.preventDefault();
  if (p && w.contains(p)) return;
  const k = !e.clientX && !e.clientY,
    r = k && e.target.getBoundingClientRect(),
    f = () => placeAt(byId(w.dataset.aiorosContext), k ? r.left : e.clientX, k ? r.bottom : e.clientY);
  e.buttons ? pointerPath = f : f();
});
doc.addEventListener("pointerup", () => {
  const f = pointerPath;
  pointerPath = 0;
  f && f();
}, true);
doc.addEventListener("pointerover", e => {
  if (e.pointerType == "touch") return;
  hoverShow(hoverOf(e.target), 1);
  const t = triggerOf(e.target),
    p = t && targetOf(t);
  if (!p) return;
  clearTimeout(t.t);
  if (t.k || p.matches(":popover-open")) return;
  const o = openSibling(t);
  o ? (W(t, o, p), show(p)) : t.t = setTimeout(() => show(p), +t.dataset.hoverOpen);
});
doc.addEventListener("pointerout", e => {
  const w = hoverOf(e.target),
    t = triggerOf(e.target);
  t && triggerOf(e.relatedTarget) !== t && (clearTimeout(t.t), t.k = 0, t.t = setTimeout(() => hide(targetOf(t)), +t.dataset.hoverClose));
  w && e.pointerType != "touch" && !w.contains(e.relatedTarget) && hoverShow(w, 0);
  const v = closest(e.target, ".tip");
  if (v && !v.contains(e.relatedTarget)) {
    delete v.dataset.off;
    const t2 = v.querySelector(":scope>.tt");
    t2 && getComputedStyle(t2).visibility == "visible" && skipTo(v);
  }
});
doc.addEventListener("focusin", e => {
  const w = hoverOf(e.target);
  w && e.target.matches(":focus-visible") && hoverShow(w, 1);
});
doc.addEventListener("focusout", e => {
  const w = hoverOf(e.target),
    v = closest(e.target, ".tip");
  w && !w.contains(e.relatedTarget) && hoverShow(w, 0);
  v && !v.contains(e.relatedTarget) && delete v.dataset.off;
});
doc.addEventListener("pointerdown", e => {
  const w = closest(e.target, "[data-aioros-click]");
  w && (w.o = byId(w.dataset.aiorosClick)?.matches(":popover-open"));
}, true);
doc.addEventListener("beforetoggle", e => {
  const p = e.target;
  e.newState == "open" && p.dataset && (p.w ? p.w = 0 : delete p.dataset.motion);
}, true);
doc.addEventListener("click", e => {
  const w = closest(e.target, "[data-aioros-click]"),
    p = closest(e.target, "[popover]"),
    t = closest(e.target, "[data-hover-open]");
  t && (clearTimeout(t.t), t.k = t.matches(":hover"));
  const o = t && !p && openSibling(t);
  o && W(t, o, targetOf(t));
  if (!w || p && w.contains(p)) return;
  const f = byId(w.dataset.aiorosClick);
  w.o ? hide(f) : show(f);
  w.o = 0;
});
var module = {};
export { module as default };
