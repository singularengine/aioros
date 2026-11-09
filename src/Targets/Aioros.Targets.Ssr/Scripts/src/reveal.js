const seen = new WeakSet(),
  reducedMotion = matchMedia("(prefers-reduced-motion: reduce)"),
  observer = new IntersectionObserver(es => {
    for (const e of es) if (e.isIntersecting) {
      observer.unobserve(e.target);
      reveal(e.target);
    }
  }, {
    rootMargin: "0px 0px -10% 0px"
  });
function reveal(e) {
  if (e.hasAttribute("data-aioros-reveal")) {
    e.setAttribute("data-aioros-revealed", "");
    for (const a of e.getAnimations()) a.play();
  }
  e.n && count(e);
}
function parse(t) {
  const m = /^(.*?)(\d[\d.,\u00a0\u202f ]*\d|\d)(.*)$/s.exec(t);
  if (!m) return null;
  const b = m[2],
    q = b.replace(/\d/g, "");
  let d = "",
    g = "";
  if (q) {
    const l = q[q.length - 1],
      u = [...new Set(q)];
    if (u.length > 1) {
      d = l;
      g = u.find(c => c !== l);
    } else if (q.length === 1 && b.length - b.lastIndexOf(l) - 1 !== 3) d = l;else g = l;
  }
  const i2 = d ? b.slice(0, b.lastIndexOf(d)) : b,
    f = d ? b.slice(b.lastIndexOf(d) + 1) : "";
  return {
    p: m[1],
    s: m[3],
    g,
    d,
    f: f.length,
    v: Number((g ? i2.split(g).join("") : i2) + (f ? "." + f : ""))
  };
}
function format(x, n) {
  const [i2, f] = n.toFixed(x.f).split(".");
  return x.p + (x.g ? i2.replace(/\B(?=(\d{3})+(?!\d))/g, x.g) : i2) + (x.f ? x.d + f : "") + x.s;
}
function count(e) {
  const x = e.n,
    ms = +e.dataset.aiorosCount || 1;
  e.n = null;
  let w = format(x, 0),
    t0;
  e.textContent = w;
  e.setAttribute("data-aioros-counting", "");
  const step = t => {
    if (e.textContent !== w) {
      e.removeAttribute("data-aioros-counting");
      return;
    }
    t0 ??= t;
    const k = Math.min(1, (t - t0) / ms);
    if (k >= 1) {
      e.textContent = e.t;
      e.removeAttribute("data-aioros-counting");
      return;
    }
    w = format(x, x.v * (1 - Math.pow(2, -10 * k)));
    e.textContent = w;
    requestAnimationFrame(step);
  };
  requestAnimationFrame(step);
}
function init() {
  document.querySelectorAll("[data-aioros-reveal],[data-aioros-count]").forEach(e => {
    if (seen.has(e)) return;
    seen.add(e);
    if (e.hasAttribute("data-aioros-count") && !reducedMotion.matches && e.childElementCount === 0) {
      e.t = e.textContent;
      e.n = parse(e.t);
    }
    const r = e.getBoundingClientRect();
    if (r.bottom > 0 && r.top < innerHeight * 0.9) {
      reveal(e);
      return;
    }
    if (e.hasAttribute("data-aioros-reveal")) for (const a of e.getAnimations()) {
      a.pause();
      a.currentTime = 0;
    }
    e.n && (e.textContent = format(e.n, 0));
    observer.observe(e);
  });
}
document.addEventListener("pointermove", e => {
  const t = e.target.closest?.("[data-aioros-spotlight]");
  if (!t) return;
  const r = t.getBoundingClientRect();
  t.style.setProperty("--x", Math.round(e.clientX - r.left) + "px");
  t.style.setProperty("--y", Math.round(e.clientY - r.top) + "px");
}, {
  passive: true
});
let parallaxFrame = 0;
const parallax = () => {
  parallaxFrame = 0;
  if (reducedMotion.matches) return;
  const middle = innerHeight / 2;
  const shifts = [...document.querySelectorAll("[data-aioros-parallax]")].map(layer => {
    const box = layer.getBoundingClientRect();
    const center = box.top + box.height / 2 - (layer.aiorosShift || 0);
    return [layer, Math.round(-(center - middle) * (+layer.dataset.aiorosParallax || 0))];
  });
  for (const [layer, shift] of shifts) {
    layer.aiorosShift = shift;
    layer.style.willChange = "translate";
    layer.style.translate = "0 " + shift + "px";
  }
};
const requestParallax = () => {
  parallaxFrame || (parallaxFrame = requestAnimationFrame(parallax));
};
addEventListener("scroll", requestParallax, {
  capture: true,
  passive: true
});
addEventListener("resize", requestParallax, {
  passive: true
});
init();
parallax();
var module = {
  i() {
    init();
    parallax();
  }
};
export { module as default };
