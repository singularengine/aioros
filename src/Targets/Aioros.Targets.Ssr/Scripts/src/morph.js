const morphAttribute = "data-aioros-morph",
  fromAttribute = morphAttribute + "-from",
  keyAttribute = morphAttribute + "-key",
  running = new Map(),
  reducedMotion = matchMedia("(prefers-reduced-motion: reduce)"),
  math = Math;
let frameRequest = 0;
const numbers = s => s ? s.trim().split(/[\s,]+/).map(Number) : [],
  setting = (e, k) => (e.closest(`[${morphAttribute}-${k}]`) || e).getAttribute(morphAttribute + "-" + k) || "";
function curve(p, k, l) {
  let d = "",
    n = 0,
    x0,
    y0,
    x1,
    y1,
    t0,
    t1;
  const Y = k == "y",
    o = (a, b) => Y ? b + "," + a : a + "," + b,
    sg = v => v < 0 ? -1 : 1,
    C = (t, u) => {
      const h = (x1 - x0) / 3;
      d += "C" + o(x0 + h, y0 + h * t) + "," + o(x1 - h, y1 - h * u) + "," + o(x1, y1);
    },
    s2 = t => {
      const h = x1 - x0;
      return h ? (3 * (y1 - y0) / h - t) / 2 : t;
    };
  for (let i = 0; i < p.length; i += 2) {
    const x = p[i + Y],
      y = p[i + !Y];
    if (!k) {
      d += (n++ || l ? "L" : "M") + o(x, y);
      continue;
    }
    if (x === x1 && y === y1) continue;
    t1 = NaN;
    if (!n) {
      n = 1;
      d += (l ? "L" : "M") + o(x, y);
    } else if (n < 2) n = 2;else {
      const h0 = x1 - x0,
        h1 = x - x1,
        s0 = (y1 - y0) / (h0 || h1 < 0 && -0),
        s1 = (y - y1) / (h1 || h0 < 0 && -0),
        q = (s0 * h1 + s1 * h0) / (h0 + h1);
      t1 = (sg(s0) + sg(s1)) * math.min(math.abs(s0), math.abs(s1), 0.5 * math.abs(q)) || 0;
      n < 3 ? (n = 3, C(s2(t1), t1)) : C(t0, t1);
    }
    x0 = x1;
    x1 = x;
    y0 = y1;
    y1 = y;
    t0 = t1;
  }
  if (k) n == 2 ? d += "L" + o(x1, y1) : n == 3 && C(t0, s2(t0));
  return n == 1 && l == null ? d + "Z" : d;
}
const shapes = {
  line: [2, "d", (v, k) => curve(v, k)],
  polygon: [2, "d", v => curve(v) + "Z"],
  rect: [4, "d", v => `M${v[0]},${v[1]}h${v[2]}v${v[3]}h${-v[2]}Z`],
  band: [4, "d", (v, k) => {
    const t = [],
      b = [];
    for (let i = 0; i < v.length; i += 4) {
      t.push(v[i], v[i + 1]);
      b.unshift(v[i + 2], v[i + 3]);
    }
    return curve(t, k, 0) + curve(b, k, 1) + "Z";
  }],
  sector: [6, "d", v => {
    const [x, y, r, o, a] = v,
      g = v[5] - a,
      d = math.sign(g) * math.min(math.abs(g), 359.999),
      e = a + d,
      L = +(math.abs(d) > 180),
      p = (r2, a2) => [x + r2 * math.cos(-a2 * math.PI / 180), y + r2 * math.sin(-a2 * math.PI / 180)];
    return `M${p(o, a)}A${o},${o},0,${L},${+(a > e)},${p(o, e)}` + (r > 0 ? `L${p(r, e)}A${r},${r},0,${L},${+(a <= e)},${p(r, a)}Z` : `L${x},${y}Z`);
  }],
  glyph: [3, "transform", (v, k, t) => {
    const s = math.sqrt(v[2] / t[2]);
    return `matrix(${s},0,0,${s},${v[0] - s * t[0]},${v[1] - s * t[1]})`;
  }]
};
function ease(s) {
  const z = /\(([^)]*)/.exec(s),
    [a, b, c, d] = z ? numbers(z[1]) : {
      linear: [0, 0, 1, 1],
      "ease-in": [0.42, 0, 1, 1],
      "ease-out": [0, 0, 0.58, 1],
      "ease-in-out": [0.42, 0, 0.58, 1]
    }[s] || [0.25, 0.1, 0.25, 1],
    B = (u, p, q) => 3 * p * u * (1 - u) ** 2 + 3 * q * u * u * (1 - u) + u ** 3;
  return t => t <= 0 || t >= 1 ? t : (() => {
    let l = 0,
      h = 1,
      u = t;
    for (let i = 0; i < 40; i++) {
      u = (l + h) / 2;
      B(u, a, c) < t ? l = u : h = u;
    }
    return B(u, b, d);
  })();
}
function stop(e) {
  running.delete(e);
  e.removeAttribute(morphAttribute + "ing");
}
function start(e, o) {
  const s = e.getAttribute(morphAttribute),
    w = setting(e, "path");
  if (o && o.s === s && o.k === e.getAttribute(keyAttribute)) return;
  stop(e);
  if (s === null || !w || reducedMotion.matches) return;
  const G = shapes[w.split(" ")[0]] || [w.split(" ").length],
    k = G[0],
    to = numbers(s),
    was = o ? o.c || numbers(o.s) : [],
    at = numbers(e.getAttribute(fromAttribute)),
    n = to.length / k | 0,
    p = was.length / k | 0,
    r = setting(e, "map") == "ratio" && p / n,
    from = new Float64Array(to.length),
    names = G[1] ? [G[1]] : w.split(" ");
  if (!o && !at.length) return;
  for (let i = 0; i < n; i++) {
    const j = r ? math.floor(i * r) : i,
      [u, x] = j < p ? [was, j] : at.length ? [at, i % (at.length / k)] : [to, i];
    for (let h = 0; h < k; h++) {
      const q = u[x * k + h];
      from[i * k + h] = q >= -1 / 0 ? q : to[i * k + h];
    }
  }
  const A = {
    e,
    to,
    from,
    c: new Float64Array(to.length),
    G,
    names,
    x: (/monotone-(x|y)/.exec(w) || [])[1],
    z: ease(setting(e, "easing")),
    t: performance.now() + +setting(e, "delay"),
    d: +setting(e, "duration")
  };
  A.end = names.map(a => e.getAttribute(a));
  running.set(e, A);
  e.setAttribute(morphAttribute + "ing", "");
  frame(A, 0);
  frameRequest || (frameRequest = requestAnimationFrame(tick));
  return 1;
}
function frame(A, t) {
  const {
      e,
      to,
      from,
      c,
      G
    } = A,
    p = A.d > 0 ? math.min(1, math.max(0, (t - A.t) / A.d)) : +(t >= A.t),
    u = A.z(p);
  for (let i = 0; i < to.length; i++) c[i] = from[i] * (1 - u) + to[i] * u;
  G[2] ? e.setAttribute(A.names[0], G[2](c, A.x, to)) : A.names.forEach((a, i) => e.setAttribute(a, c[i]));
  return p >= 1;
}
function tick() {
  const t = performance.now();
  for (const A of running.values()) if (!A.e.isConnected || frame(A, t)) {
    A.names.forEach((a, i) => A.end[i] === null ? A.e.removeAttribute(a) : A.e.setAttribute(a, A.end[i]));
    stop(A.e);
  }
  frameRequest = running.size ? requestAnimationFrame(tick) : 0;
}
let queued = null;
function flush() {
  const q = queued,
    s = new Set(),
    a = [];
  queued = null;
  for (const [x, e] of q) if (start(e)) for (let n = e; n; n = n != x && n.parentElement) s.add(n);
  s.forEach(n => a.push(...n.getAnimations()));
  a.forEach(z => z.finish());
}
var module = {
  o: (a, b) => b.hasAttribute(morphAttribute) && {
    s: a.getAttribute(morphAttribute),
    k: a.getAttribute(keyAttribute),
    c: running.get(a)?.c
  },
  v: start,
  i: x => {
    if (!x.querySelectorAll) return;
    const l = [x, ...x.querySelectorAll(`[${fromAttribute}]`)].filter(e => e.hasAttribute(fromAttribute) && !x.contains(e.parentElement?.closest(`[${morphAttribute}-path]`)));
    if (!l.length) return;
    if (!queued) {
      queued = [];
      queueMicrotask(flush);
    }
    l.forEach(e => queued.push([x, e]));
  }
};
export { module as default };
