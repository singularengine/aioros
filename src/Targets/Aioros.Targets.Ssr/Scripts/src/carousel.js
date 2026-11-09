const doc = document,
  carouselSelector = "[data-aioros-carousel]",
  stripOf = c => c.matches("[data-aioros-strip]") ? c : c.querySelector("[data-aioros-strip]"),
  slidesOf = c => [...stripOf(c).children],
  ownElements = (c, q) => [...c.querySelectorAll(q)].filter(x => x.closest(carouselSelector) == c),
  selectedIndex = c => Math.max(0, slidesOf(c).findIndex(x => x.classList.contains("sel"))),
  wraps = c => c.hasAttribute("data-aioros-wrap"),
  isVertical = c => c.dataset.aiorosCarousel == "v",
  snapAlign = x => getComputedStyle(x).scrollSnapAlign.includes("center") ? 0.5 : 0,
  offsetOf = (c, x) => {
    const r = x.getBoundingClientRect(),
      q = stripOf(c).getBoundingClientRect(),
      m = snapAlign(x);
    return isVertical(c) ? r.top - q.top - (q.height - r.height) * m : r.left - q.left - (q.width - r.width) * m;
  },
  nearestIndex = c => {
    const l = slidesOf(c);
    let k2 = 0;
    l.forEach((x, j) => Math.abs(offsetOf(c, x)) < Math.abs(offsetOf(c, l[k2])) && (k2 = j));
    return k2;
  },
  markSelected = (c, n) => {
    const l = slidesOf(c),
      j = selectedIndex(c),
      w = wraps(c);
    if (n != j && l[n]) {
      l[j]?.classList.remove("sel");
      l[n].classList.add("sel");
      const h = l[j]?.querySelector(":scope>input[type=hidden]");
      if (h) {
        l[n].prepend(h);
        h.value = n;
        h.dispatchEvent(new Event("change", {
          bubbles: true
        }));
      }
      const t = ownElements(c, "[data-aioros-slide-to]");
      if (t[j] && t[n]) {
        const a = t[j].className;
        t[j].className = t[n].className;
        t[n].className = a;
        t[j].removeAttribute("aria-current");
        t[n].setAttribute("aria-current", "true");
      }
    }
    ownElements(c, '[data-aioros-slide="-1"]').forEach(b => b.disabled = !w && n == 0);
    ownElements(c, '[data-aioros-slide="1"]').forEach(b => b.disabled = !w && n == l.length - 1);
  },
  goTo = (c, n, i2) => {
    const l = slidesOf(c),
      z = l.length;
    if (!z) return;
    n = wraps(c) ? (n % z + z) % z : Math.min(Math.max(n, 0), z - 1);
    c._t = n;
    stripOf(c).scrollBy({
      [isVertical(c) ? "top" : "left"]: offsetOf(c, l[n]),
      behavior: i2 || matchMedia("(prefers-reduced-motion: reduce)").matches ? "instant" : "smooth"
    });
    markSelected(c, n);
  },
  init = () => doc.querySelectorAll(carouselSelector).forEach(c => {
    if (c._i) markSelected(c, c._t ?? nearestIndex(c));else {
      c._i = 1;
      goTo(c, selectedIndex(c), 1);
    }
  });
doc.addEventListener("click", e => {
  const b = e.target.closest?.("[data-aioros-slide],[data-aioros-slide-to]"),
    c = b?.closest(carouselSelector);
  if (!c || b.disabled) return;
  goTo(c, b.hasAttribute("data-aioros-slide-to") ? ownElements(c, "[data-aioros-slide-to]").indexOf(b) : selectedIndex(c) + +b.dataset.aiorosSlide);
});
doc.addEventListener("keydown", e => {
  const c = e.target.closest?.(carouselSelector),
    s = {
      ArrowLeft: -1,
      ArrowRight: 1
    }[e.key];
  if (!c || !s || e.ctrlKey || e.metaKey || e.altKey || e.target.closest("input,textarea,select,[contenteditable]")) return;
  e.preventDefault();
  goTo(c, selectedIndex(c) + s);
});
doc.addEventListener("scrollend", e => {
  const c = e.target.closest?.(carouselSelector);
  if (c && stripOf(c) == e.target) {
    if (c._d == 2) release(c);
    if (!c._d) {
      c._t = null;
      markSelected(c, nearestIndex(c));
    }
  }
}, true);
let pressed = null;
const pointerAxis = (e, v) => v ? e.clientY : e.clientX,
  release = c => {
    const s = stripOf(c);
    c._d = 0;
    s.style.scrollSnapType = "";
    s.style.scrollBehavior = "";
  };
doc.addEventListener("pointerdown", e => {
  const c = e.target.closest?.(carouselSelector + "[data-aioros-drag]");
  if (!c || e.pointerType != "mouse" || e.button || e.target.closest("input,textarea,select,[contenteditable]") || !stripOf(c).contains(e.target)) return;
  const s = stripOf(c),
    v = isVertical(c),
    n = {
      p: pointerAxis(e, v),
      t: performance.now()
    };
  pressed = {
    c,
    s,
    v,
    a: n.p,
    o: v ? s.scrollTop : s.scrollLeft,
    f: n,
    l: n,
    m: 0
  };
}, true);
doc.addEventListener("pointermove", e => {
  if (!pressed) return;
  const q = pointerAxis(e, pressed.v) - pressed.a;
  if (Math.abs(q) > 10) pressed.m = 1;
  if (!pressed.c._d && q) {
    pressed.c._d = 1;
    pressed.s.style.scrollSnapType = "none";
    pressed.s.style.scrollBehavior = "auto";
  }
  pressed.s[pressed.v ? "scrollTop" : "scrollLeft"] = pressed.o - q;
  const n = {
    p: pointerAxis(e, pressed.v),
    t: performance.now()
  };
  if (n.t - pressed.f.t > 170) pressed.f = n;
  pressed.l = n;
});
doc.addEventListener("pointerup", e => {
  if (!pressed) return;
  const {
    c,
    s,
    m,
    f,
    l,
    v
  } = pressed;
  pressed = null;
  if (!c._d) return;
  const u = performance.now(),
    t = u - f.t,
    r = t && u - l.t <= 170 ? (l.p - f.p) / t : 0,
    w = (Math.abs(r) > 0.1 ? r : 0) * 300,
    b = v ? s.clientHeight : s.clientWidth,
    h = Math.min(Math.max(b * 0.2, 50), 225),
    x = slidesOf(c);
  let n = selectedIndex(c);
  if (Math.abs(w) >= h) n = selectedIndex(c) - Math.sign(w);else {
    let j = 0;
    x.forEach((y, o2) => Math.abs(offsetOf(c, y) + w) < Math.abs(offsetOf(c, x[j]) + w) && (j = o2));
    n = j;
  }
  c._d = 2;
  const o = slidesOf(c)[wraps(c) ? (n % x.length + x.length) % x.length : Math.min(Math.max(n, 0), x.length - 1)];
  Math.abs(offsetOf(c, o)) < 1 ? release(c) : 0;
  goTo(c, n);
  if (m) {
    const k2 = y => {
      y.preventDefault();
      y.stopPropagation();
    };
    doc.addEventListener("click", k2, {
      capture: true,
      once: true
    });
    setTimeout(() => doc.removeEventListener("click", k2, true));
  }
});
doc.addEventListener("dragstart", e => e.target.closest?.(carouselSelector + "[data-aioros-drag] [data-aioros-strip]," + carouselSelector + "[data-aioros-drag][data-aioros-strip]") && e.preventDefault());
init();
var module = {
  i: init
};
export { module as default };
