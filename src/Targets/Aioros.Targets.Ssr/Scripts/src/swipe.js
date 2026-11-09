const doc = document,
  deckSelector = ".notice[data-aioros-deck]",
  activeNotices = g => [...g.children].filter(x => x.classList.contains("nt") && !x.hasAttribute("data-gone")),
  isExpanded = g => !!g.h || g.hasAttribute("data-aioros-expanded"),
  naturalHeight = x => {
    const s = x.style,
      h = s.height,
      t = s.transition;
    s.transition = "none";
    s.height = "";
    const n = parseFloat(getComputedStyle(x).height);
    s.height = h;
    void x.offsetHeight;
    s.transition = t;
    return n || +x.dataset.h;
  },
  styleSources = () => [...doc.querySelectorAll("style[data-aioros-style],head [data-css]")],
  heldStyles = () => {
    const c = [...doc.querySelectorAll(deckSelector + ">.nt")];
    doc.querySelectorAll("[data-aioros-kept]").forEach(s => c.some(x => x._y?.includes(s)) || s.remove());
  },
  layout = g => {
    if (!g.isConnected) return;
    const [p, v, s] = g.dataset.aiorosDeck.split(" ").map(Number),
      t = g.dataset.aiorosAnchor == "top" ? 1 : -1,
      e = isExpanded(g),
      c = activeNotices(g).reverse(),
      h = c.map(naturalHeight),
      f = h[0] || 0;
    let y = 0,
      z = 0;
    c.forEach((x, i2) => {
      const b = !e && i2 > 0;
      x.style.translate = "0 " + t * (e ? y : p * i2) + "px";
      x.style.scale = b ? String(1 - s * i2) : "";
      x.style.height = b ? f + "px" : "";
      x.toggleAttribute("data-behind", b);
      x.style.opacity = i2 < v ? "" : "0";
      x.style.zIndex = c.length - i2;
      if (i2 < v) z += h[i2] + (i2 ? p : 0);
      y += h[i2] + p;
    });
    g.style.height = (e ? z : f) + "px";
    const q = e || g.p || doc.hidden;
    c.forEach(x => x.style.animationPlayState = q ? "paused" : "");
  },
  expand = g => {
    if (!activeNotices(g).length && ![...g.children].some(x => x.hasAttribute("data-gone"))) {
      try {
        g.hidePopover();
      } catch {}
      g.remove();
    }
    heldStyles();
  },
  dismiss = (x, o) => {
    if (x.hasAttribute("data-gone")) return;
    const g = x.parentElement;
    x.setAttribute("data-gone", "");
    x.style.animationPlayState = "running";
    if (o) x.dataset.out = o;else {
      x.style.setProperty("--expire", "0ms");
      x.style.animation = "none";
      void x.offsetWidth;
      x.style.animation = "";
      if (getComputedStyle(x).animationName == "none") x.remove();
    }
    g.matches(deckSelector) ? layout(g) : 0;
    expand(g);
  },
  bind = g => {
    if (g._i) return;
    g._i = 1;
    g.addEventListener("pointerenter", () => {
      g.h = 1;
      layout(g);
    });
    g.addEventListener("pointerleave", () => {
      if (!g.p) {
        g.h = 0;
        layout(g);
      }
    });
    g.addEventListener("pointerdown", () => {
      g.p = 1;
      layout(g);
    });
  },
  init = () => {
    const k = [...doc.querySelectorAll(deckSelector)],
      g = k.find(x => activeNotices(x).length) || k[0],
      y = styleSources();
    k.forEach(o => [...o.children].forEach(x => x._y ??= y));
    k.forEach(o => {
      if (o != g) {
        activeNotices(o).forEach(x => {
          o.dataset.replay && (x.dataset.replay ??= o.dataset.replay);
          g.append(x);
        });
        o.remove();
      }
    });
    if (g) {
      bind(g);
      try {
        g.showPopover();
      } catch {}
      layout(g);
    }
  },
  append = (p, n) => {
    const g = [...doc.querySelectorAll(deckSelector)].find(x => activeNotices(x).length);
    if (!g) {
      p.appendChild(n.cloneNode(true));
      init();
      return;
    }
    const s = styleSources();
    [...n.children].forEach(x => {
      const y = x.cloneNode(true);
      y._y = s;
      n.dataset.replay && (y.dataset.replay ??= n.dataset.replay);
      g.append(y);
    });
    try {
      g.showPopover();
    } catch {}
    layout(g);
  };
doc.addEventListener("animationend", e => {
  const x = e.target,
    g = x.parentElement;
  if (!g?.classList.contains("notice") || !x.classList.contains("nt")) return;
  x.remove();
  g.matches(deckSelector) && layout(g);
  expand(g);
}, true);
doc.addEventListener("click", e => {
  const b = e.target.closest?.("button"),
    x = b?.closest(".notice>.nt");
  if (!x) return;
  if (b.hasAttribute("data-aioros-closes")) {
    e.preventDefault();
    dismiss(x);
    return;
  }
  x.parentElement.matches(deckSelector) && b.type == "submit" && queueMicrotask(() => dismiss(x));
});
doc.addEventListener("visibilitychange", () => doc.querySelectorAll(deckSelector).forEach(layout));
let swiping = null;
const swipeSelector = "[data-aioros-swipe]",
  rubberBand = v => 8 * (Math.log(v + 1) - 2),
  follow = (w, e) => {
    const x = w.closest(".notice>.nt"),
      g = x ? null : w.closest("dialog");
    return {
      w,
      x,
      g,
      m: g?.querySelector(".pm"),
      r: w.dataset.aiorosSwipe.split(" "),
      b: w.dataset.aiorosSwipeBy.split(" ").map(Number),
      x0: e.clientX,
      y0: e.clientY,
      t0: performance.now(),
      k: 0,
      ax: 0,
      ay: 0,
      n: 0
    };
  };
doc.addEventListener("pointerdown", e => {
  if (e.button) return;
  const w = e.target.closest?.(swipeSelector);
  if (!w || e.target.closest("input,textarea,select,[contenteditable]") || e.target.closest(".notice") && e.target.closest("button,a")) return;
  for (let p = e.target; p && p != w; p = p.parentElement) if (p.scrollTop > 0 || p.scrollLeft > 0) return;
  swiping = follow(w, e);
}, true);
doc.addEventListener("pointermove", e => {
  if (!swiping) return;
  const dx = e.clientX - swiping.x0,
    dy = e.clientY - swiping.y0;
  if (!swiping.k && (Math.abs(dx) > 1 || Math.abs(dy) > 1)) swiping.k = Math.abs(dx) > Math.abs(dy) ? "x" : "y";
  if (!swiping.k) return;
  if (!swiping.n && Math.abs(dx) + Math.abs(dy) > 4) {
    swiping.n = 1;
    try {
      swiping.w.setPointerCapture(e.pointerId);
    } catch {}
    getSelection()?.removeAllRanges();
    swiping.w.style.userSelect = "none";
  }
  if (swiping.x) {
    const q2 = swiping.k == "x" ? dx : dy,
      [u, o] = swiping.k == "x" ? ["right", "left"] : ["down", "up"],
      ok = swiping.r.includes(q2 > 0 ? u : o),
      on = swiping.r.includes(u) || swiping.r.includes(o),
      p = q2 / (1.5 + Math.abs(q2) / 20),
      z2 = ok ? q2 : on ? Math.abs(p) < Math.abs(q2) ? p : q2 : 0;
    swiping.ax = swiping.k == "x" ? z2 : 0;
    swiping.ay = swiping.k == "y" ? z2 : 0;
    swiping.x.setAttribute("data-aioros-swiping", "");
    swiping.x.style.transform = `translate(${swiping.ax}px,${swiping.ay}px)`;
    swiping.x.style.setProperty("--sx", swiping.ax + "px");
    swiping.x.style.setProperty("--sy", swiping.ay + "px");
    return;
  }
  const r = swiping.r[0],
    y = r == "up" || r == "down",
    q = (y ? dy : dx) * (r == "up" || r == "left" ? -1 : 1),
    z = q > 0 ? q : -Math.max(rubberBand(-q), 0),
    sz = y ? swiping.w.offsetHeight : swiping.w.offsetWidth;
  swiping.az = z;
  swiping.q = q;
  swiping.w.style.transition = "none";
  swiping.w.style.translate = y ? `0 ${z * (r == "up" ? -1 : 1)}px` : `${z * (r == "left" ? -1 : 1)}px 0`;
  if (swiping.m) {
    swiping.m.style.transition = "none";
    swiping.m.style.opacity = String(1 - Math.max(z, 0) / sz);
  }
});
const release = e => {
  if (!swiping) return;
  const s = swiping;
  swiping = null;
  s.w.style.userSelect = "";
  const t = Math.max(performance.now() - s.t0, 1),
    [dist, vel] = s.b;
  if (s.n) {
    const k = c => {
      c.preventDefault();
      c.stopPropagation();
    };
    doc.addEventListener("click", k, {
      capture: true,
      once: true
    });
    setTimeout(() => doc.removeEventListener("click", k, true));
  }
  if (s.x) {
    const q = s.k == "x" ? s.ax : s.ay,
      sz2 = s.k == "x" ? s.x.offsetWidth : s.x.offsetHeight,
      lim2 = dist < 1 ? dist * sz2 : dist;
    s.x.removeAttribute("data-aioros-swiping");
    if (s.k && (Math.abs(q) >= lim2 || Math.abs(q) / t > vel)) dismiss(s.x, s.k == "x" ? q > 0 ? "right" : "left" : q > 0 ? "down" : "up");else {
      s.x.style.transform = "";
      s.x.style.removeProperty("--sx");
      s.x.style.removeProperty("--sy");
    }
    return;
  }
  const r = s.r[0],
    y = r == "up" || r == "down",
    sz = y ? s.w.offsetHeight : s.w.offsetWidth,
    lim = dist < 1 ? dist * sz : dist,
    go = (s.q || 0) > 0 && ((s.q || 0) / t > vel || (s.az || 0) >= lim);
  s.w.style.transition = "";
  s.m && (s.m.style.transition = "", s.m.style.opacity = "");
  if (go && s.g) s.g.close();else s.w.style.translate = "";
};
doc.addEventListener("pointerup", release, true);
doc.addEventListener("pointercancel", release, true);
doc.addEventListener("dragstart", e => swiping && e.preventDefault(), true);
doc.addEventListener("pointerup", () => doc.querySelectorAll(deckSelector).forEach(g => {
  if (g.p) {
    g.p = 0;
    if (!g.matches(":hover")) g.h = 0;
    layout(g);
  }
}), true);
doc.addEventListener("toggle", e => {
  e.newState == "open" && e.target.querySelectorAll?.(swipeSelector).forEach(w => w.style.translate = "");
}, true);
init();
var module = {
  i: init,
  a: append
};
export { module as default };
