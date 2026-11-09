const doc = document,
  rangeSelector = "[data-aioros-range]",
  rangeOf = g => {
    const [a, b] = g.querySelectorAll(":scope>input[type=range]"),
      s = a.step === "any" ? 0 : +a.step;
    return {
      a,
      b,
      s,
      w: (a.step.split(".")[1] || "").length,
      lo: +a.min,
      hi: +a.max,
      v: getComputedStyle(a).writingMode.startsWith("vertical")
    };
  },
  snap = (z, x) => {
    const y = z.s ? Math.round((x - z.lo) / z.s) * z.s + z.lo : x;
    return Math.min(z.hi, Math.max(z.lo, +y.toFixed(z.s ? z.w : 10)));
  },
  update = (g, x, i) => {
    const z = rangeOf(g),
      e = [z.a, z.b],
      y = snap(z, x),
      m = e.map(t => +t.value);
    m[i] = y;
    m.sort((p, q) => p - q);
    const j = m.indexOf(y);
    e.forEach((t, k) => {
      if (+t.value !== m[k]) {
        t.value = m[k];
        t.dispatchEvent(new Event("input", {
          bubbles: true
        }));
        t.dispatchEvent(new Event("change", {
          bubbles: true
        }));
      }
    });
    e[j].focus({
      preventScroll: true
    });
    return j;
  },
  valueAt = (g, f, z) => {
    const r = g.getBoundingClientRect(),
      t = z.v ? (r.bottom - f.clientY) / r.height : (f.clientX - r.left) / r.width;
    return z.lo + Math.min(1, Math.max(0, t)) * (z.hi - z.lo);
  };
doc.addEventListener("pointerdown", e => {
  const g = e.target.closest?.(rangeSelector);
  if (!g || e.button) return;
  const z = rangeOf(g);
  if (z.a.disabled) return;
  e.preventDefault();
  const x = valueAt(g, e, z);
  let j = update(g, x, Math.abs(+z.b.value - x) < Math.abs(+z.a.value - x) ? 1 : 0);
  g.setPointerCapture(e.pointerId);
  const m = f => {
      if (g.hasPointerCapture(f.pointerId)) j = update(g, valueAt(g, f, z), j);
    },
    w = () => {
      g.removeEventListener("pointermove", m);
      g.removeEventListener("lostpointercapture", w);
    };
  g.addEventListener("pointermove", m);
  g.addEventListener("lostpointercapture", w);
});
doc.addEventListener("keydown", e => {
  const t = e.target,
    g = t.parentElement;
  if (t.type !== "range" || !g?.matches(rangeSelector) || e.ctrlKey || e.metaKey || e.altKey) return;
  const z = rangeOf(g),
    i = t === z.a ? 0 : 1,
    k = e.key,
    c = +t.value,
    s = (z.s || 1) * (k === "PageUp" || k === "PageDown" || e.shiftKey && k.startsWith("Arrow") ? 10 : 1),
    f = {
      Home: [z.lo, 0],
      End: [z.hi, 1],
      ArrowRight: [c + s, i],
      ArrowUp: [c + s, i],
      PageUp: [c + s, i],
      ArrowLeft: [c - s, i],
      ArrowDown: [c - s, i],
      PageDown: [c - s, i]
    }[k];
  if (!f) return;
  e.preventDefault();
  update(g, f[0], f[1]);
});
var module = {};
export { module as default };
