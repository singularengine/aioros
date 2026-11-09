const doc = document,
  splitSelector = "[data-aioros-split]",
  splitOf = s => {
    const [a, b, c] = s.dataset.aiorosSplit.split(" "),
      g = s.parentElement,
      p = a == "c" ? "gridTemplateColumns" : "gridTemplateRows",
      t = getComputedStyle(g)[p].split(" ").map(parseFloat),
      k = s.dataset.aiorosTracks.split(" "),
      n = k.reduce((m, x2, i2) => x2 == "s" ? m + t[i2] : m, 0),
      x = (s.dataset.aiorosPx || "0 Infinity 0 Infinity").split(" ").map(Number),
      w = t[b] + t[c],
      v = q => +s.getAttribute(q) * n / 100;
    return {
      b,
      c,
      g,
      p,
      t,
      k,
      n,
      w,
      lo: Math.max(v("aria-valuemin"), x[0], w - x[3]),
      hi: Math.min(v("aria-valuemax"), x[1], w - x[2])
    };
  },
  fieldOf = s => s.querySelector(":scope>input[type=hidden]"),
  post = s => fieldOf(s)?.dispatchEvent(new Event("input", {
    bubbles: true
  })),
  updateValues = g => g.querySelectorAll(":scope>" + splitSelector).forEach(s => {
    const z = splitOf(s);
    if (!z.n) return;
    const v = Math.round(z.t[z.b] / z.n * 1e3) / 10,
      f = fieldOf(s);
    s.setAttribute("aria-valuenow", v);
    if (f) f.value = v;
  }),
  resize = (s, v, f) => {
    const z = splitOf(s),
      t = z.t;
    v = f ? Math.min(Math.max(v, 0), z.w) : Math.min(Math.max(v, z.lo), Math.max(z.lo, z.hi));
    t[z.c] = z.w - v;
    t[z.b] = v;
    z.g.style[z.p] = z.k.map((x, i2) => x == "s" ? "minmax(0," + t[i2] + "fr)" : x == "a" ? "auto" : x).join(" ");
    z.g.setAttribute("data-aioros-sized", "");
    updateValues(z.g);
  },
  init = () => doc.querySelectorAll(splitSelector).forEach(s => updateValues(s.parentElement));
doc.addEventListener("pointerdown", e => {
  const s = e.target.closest?.(splitSelector);
  if (!s || e.button) return;
  e.preventDefault();
  s.focus();
  s.setPointerCapture(e.pointerId);
  const h = s.getAttribute("aria-orientation") == "vertical",
    a = h ? e.clientX : e.clientY,
    v = splitOf(s).t[splitOf(s).b],
    m = f => {
      if (s.hasPointerCapture(f.pointerId)) resize(s, v + (h ? f.clientX : f.clientY) - a);
    },
    z = () => {
      s.removeEventListener("pointermove", m);
      s.removeEventListener("lostpointercapture", z);
      post(s);
    };
  s.addEventListener("pointermove", m);
  s.addEventListener("lostpointercapture", z);
});
doc.addEventListener("keydown", e => {
  const s = e.target.closest?.(splitSelector);
  if (!s || e.target != s || e.ctrlKey || e.metaKey || e.altKey) return;
  const z = splitOf(s),
    h = s.getAttribute("aria-orientation") == "vertical",
    p = s.dataset.aiorosStep,
    j = p.endsWith("%") ? parseFloat(p) * z.n / 100 : parseFloat(p),
    v = z.t[z.b],
    c = s.dataset.aiorosCollapse;
  if (e.key == "F6") {
    const l = [...z.g.querySelectorAll(":scope>" + splitSelector)],
      k = l.indexOf(s);
    l[(k + (e.shiftKey ? l.length - 1 : 1)) % l.length].focus();
    e.preventDefault();
    return;
  }
  if (e.key == "Enter") {
    if (c == null) return;
    const y = c * z.n / 100;
    Math.abs(v - y) < 0.5 ? resize(s, s._l ?? z.hi, 1) : (s._l = v, resize(s, y, 1));
    e.preventDefault();
    post(s);
    return;
  }
  const n = {
    Home: z.lo,
    End: z.hi,
    [h ? "ArrowLeft" : "ArrowUp"]: v - j,
    [h ? "ArrowRight" : "ArrowDown"]: v + j
  }[e.key];
  if (n == null) return;
  e.preventDefault();
  resize(s, n);
  post(s);
});
init();
var module = {
  i: init
};
export { module as default };
