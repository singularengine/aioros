// Same simulated feed as the POC and Blazor versions (same algorithm, sizes and update rules), shared by the
// page and the SSE route through globalThis. One JSON message per tick is encoded once and sent to everyone.
const POINTS = 60, CW = 600, CH = 180, BAR_MAX = 130;
const CUSTOMERS = ["Ada Lovelace", "Alan Turing", "Grace Hopper", "Linus Torvalds", "Margaret Hamilton", "Dennis Ritchie",
  "Barbara Liskov", "Ken Thompson", "Frances Allen", "Donald Knuth", "Radia Perlman", "John McCarthy"];

function mulberry32(a) {
  return () => { a |= 0; a = a + 0x6D2B79F5 | 0; let t = Math.imul(a ^ a >>> 15, 1 | a);
    t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; };
}
const n0 = v => Math.round(v).toLocaleString("en-US");
const pct = (now, before) => { const p = before === 0 ? 0 : (now - before) / before * 100;
  return (p > 0 ? "+" : "") + p.toFixed(2) + "%"; };
const pad = (v, n = 2) => String(v).padStart(n, "0");
const clamp = (v, lo, hi) => Math.min(hi, Math.max(lo, v));

function createFeed() {
  const rnd = mulberry32(42);
  const next = (lo, hi) => lo + Math.floor(rnd() * (hi - lo));
  const traffic = Array.from({ length: POINTS }, (_, i) => 520 + 120 * Math.sin(i / 6) + next(-40, 40));
  const sales = Array.from({ length: 12 }, () => 30 + next(0, 70));
  let revenue = 128430, prevRevenue = revenue, orderCount = 1204, prevOrders = orderCount, nextOrder = 10240, prevConv = 3.2;
  const newOrder = () => {
    const r = rnd(), status = r < 0.7 ? "paid" : r < 0.9 ? "pending" : "failed";
    const amount = 20 + next(0, 480) + next(0, 100) / 100;
    return { id: "#" + nextOrder++, customer: CUSTOMERS[next(0, CUSTOMERS.length)], amount: "$" + amount.toFixed(2), status };
  };
  const orders = Array.from({ length: 8 }, newOrder);
  const subscribers = new Set();
  const encoder = new TextEncoder();
  const feed = { snapshot: null, message: null, subscribe(fn) { subscribers.add(fn); return () => subscribers.delete(fn); } };

  function tick() {
    traffic.copyWithin(0, 1); traffic[POINTS - 1] = clamp(traffic[POINTS - 2] + next(-60, 61), 150, 950);
    const h = next(0, 12); sales[h] = clamp(sales[h] + next(-15, 16), 5, 100);
    if (rnd() < 0.4) {
      const o = newOrder(); orders.unshift(o); if (orders.length > 8) orders.pop();
      orderCount++; revenue += parseFloat(o.amount.slice(1));
    }
    const conv = Math.round(clamp(prevConv + (rnd() - 0.5) * 0.2, 1.5, 6) * 100) / 100;
    const t = traffic[POINTS - 1], tPrev = traffic[POINTS - 2];
    const kpis = [
      { name: "Revenue", value: "$" + n0(revenue), delta: pct(revenue, prevRevenue), up: revenue >= prevRevenue },
      { name: "Orders", value: n0(orderCount), delta: pct(orderCount, prevOrders), up: orderCount >= prevOrders },
      { name: "Active users", value: n0(t), delta: pct(t, tPrev), up: t >= tPrev },
      { name: "Conversion", value: conv.toFixed(2) + "%", delta: pct(conv, prevConv), up: conv >= prevConv },
    ];
    prevRevenue = revenue; prevOrders = orderCount; prevConv = conv;
    const line = traffic.map((v, i) => `${+(i * CW / (POINTS - 1)).toFixed(2)},${+(CH - v / 1000 * (CH - 10)).toFixed(2)}`).join(" ");
    const now = new Date();
    feed.snapshot = {
      kpis, line, area: `${line} ${CW},${CH} 0,${CH}`, trafficNow: n0(t) + " /min",
      bars: sales.map((v, i) => ({ label: pad(8 + i) + "h", height: Math.round(v / 100 * BAR_MAX * 10) / 10, value: (v * 0.42).toFixed(1) + "k" })),
      orders: orders.slice(),
      updated: `Updated ${pad(now.getHours())}:${pad(now.getMinutes())}:${pad(now.getSeconds())}.${pad(now.getMilliseconds(), 3)}`,
    };
    feed.message = encoder.encode(`data: ${JSON.stringify(feed.snapshot)}\n\n`);
    for (const fn of subscribers) fn(feed.message);
  }
  tick();
  setInterval(tick, Number(process.env.POC_TICK_MS ?? 1000));
  return feed;
}

export function getFeed() {
  return (globalThis.__dashboardFeed ??= createFeed());
}
