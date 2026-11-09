"""Markdown report of bench/app.sh: every metric is the median over the runs of a variant, with its spread.
usage: python3 app_report.py app.jsonl [env.json]
A cell reads "median ±spread": the spread is half the range (max - min) over the runs, as a percentage of the median,
so one outlier run shows. Latencies are the median, over the runs, of each run's percentile."""
import json, os, sys
from statistics import median

rows = [json.loads(l) for l in open(sys.argv[1]) if l.strip()]
env = json.load(open(sys.argv[2])) if len(sys.argv) > 2 and os.path.exists(sys.argv[2]) else None
names = list(dict.fromkeys(r["name"] for r in rows))
runs = {n: [r for r in rows if r["name"] == n] for n in names}


def values(name, f):
    out = []
    for r in runs[name]:
        try:
            v = f(r)
        except (KeyError, TypeError, StopIteration):
            v = None
        if v is not None:
            out.append(v)
    return out


def agg(name, f, fmt="{:,.0f}", unit="", spread=True):
    v = values(name, f)
    if not v:
        return "–"
    m = median(v)
    text = fmt.format(m) + unit
    if spread and len(v) > 1 and m:
        text += f" ±{(max(v) - min(v)) / 2 / abs(m) * 100:.0f}%"
    return text


def table(title, metrics, note=None):
    print(f"\n### {title}\n")
    if note:
        print(note + "\n")
    print("| Metric | " + " | ".join(names) + " |")
    print("|---|" + "---|" * len(names))
    for label, cell in metrics:
        print(f"| {label} | " + " | ".join(cell(n) for n in names) + " |")


n_runs = {n: len(runs[n]) for n in names}
if env and env.get("profile") == "quick":
    print("> **Quick profile: trends only, not for publishing.** Short warm-up and measurement, 64 connections only, "
          "one open-loop page, browser metrics from a single run per variant (docs/benchmarks.md, \"Quick profile\").\n")
print(f"Lumen (a Linear-style SaaS app) on {len(names)} variants, {max(n_runs.values())} interleaved run(s) each.")
if env:
    print(f"\n{env.get('cpu')} ({env.get('cores')} threads, {env.get('memGb')} GB), Linux {env.get('kernel')}, .NET {env.get('dotnet')}, "
          f"Node {env.get('node')}, Next.js {env.get('next')}, {env.get('go') or 'no Go'} (templ {env.get('templ') or '?'}), "
          f"{env.get('rust') or 'no Rust'} (Leptos {env.get('leptos') or '?'}), {env.get('chromium')}, commit {env.get('git')}. Server on cores "
          f"{env.get('serverCpus')}, load generator and browsers on {env.get('clientCpus')}; Next.js with {env.get('nextWorkers')} worker(s); {env.get('http')}.")
loads = [float(r["loadStart"].split()[0]) for r in rows if r.get("loadStart")]
if loads:
    print(f"\n1-minute load at the start of the runs: {min(loads):.2f} to {max(loads):.2f}.")

table("Server", [
    ("cold start (until the first 200)", lambda n: agg(n, lambda r: r["coldStartMs"], unit=" ms")),
    ("first visit to /projects", lambda n: agg(n, lambda r: r["firstVisitMs"], unit=" ms")),
    ("idle memory", lambda n: agg(n, lambda r: r["idleRssMb"], unit=" MB")),
    ("memory after the load test", lambda n: agg(n, lambda r: r["afterRssMb"], unit=" MB")),
])

pages = [s["page"] for s in rows[0]["ssr"]]
ssr = lambda r, p: next(s for s in r["ssr"] if s["page"] == p)
lat = lambda key, p, q: (lambda n: agg(n, lambda r: ssr(r, p)[key][q], fmt="{:.2f}", spread=False))
table("SSR, closed loop: req/s with 64 connections (latency p50 / p99)",
      [(p, (lambda p: lambda n: f'{agg(n, lambda r: ssr(r, p)["c64"]["rps"])} ({lat("c64", p, "p50")(n)} / {lat("c64", p, "p99")(n)} ms)')(p)) for p in pages],
      "Maximum throughput. Closed-loop latencies suffer coordinated omission; see the open-loop table for latency.")
if any(s.get("c1") for r in rows for s in r["ssr"]):
    table("SSR, closed loop: req/s with 1 connection", [(p, (lambda p: lambda n: agg(n, lambda r: ssr(r, p)["c1"]["rps"]))(p)) for p in pages])

rate_pages = [s["page"] for s in rows[0]["ssr"] if s.get("rates")]
for p in rate_pages:
    pcts = [x["pct"] for x in ssr(rows[0], p)["rates"]]
    rate = lambda r, p, pct: next(x for x in ssr(r, p)["rates"] if x["pct"] == pct)
    table(f"SSR, open loop at a constant rate: {p} (rate: p50 / p99 / p99.9 ms)", [
        (f"{pct}% of the maximum", (lambda pct: lambda n:
            f'{agg(n, lambda r: rate(r, p, pct)["targetRps"], spread=False)}/s: '
            f'{agg(n, lambda r: rate(r, p, pct)["p50"], fmt="{:.2f}", spread=False)} / '
            f'{agg(n, lambda r: rate(r, p, pct)["p99"], fmt="{:.2f}", spread=False)} / '
            f'{agg(n, lambda r: rate(r, p, pct)["p999"], fmt="{:.2f}", spread=False)}'
            + (" (errors)" if any(rate(r, p, pct)["errors"] for r in runs[n]) else ""))(pct)) for pct in pcts],
        "Each request is timed from its intended send time (wrk2), so a stall counts against every request it delays.")

table("HTML per page (gzip) and the encoding sent to browsers",
      [(p, (lambda p: lambda n: f'{agg(n, lambda r: ssr(r, p)["html"] / 1024, fmt="{:.1f}", spread=False)} KB '
                                f'({agg(n, lambda r: ssr(r, p)["gzip"] / 1024, fmt="{:.1f}", spread=False)} KB, '
                                f'{ssr(runs[n][0], p).get("encoding") or "none"})')(p)) for p in pages])

profiles = list(dict.fromkeys(k for r in rows for k in r.get("browser", {})))
for prof in profiles:
    b = lambda r, p, prof=prof: r["browser"][prof]["pages"][p]
    label = {"desktop": "desktop, unthrottled", "mobile": "Lighthouse mobile: 4x CPU, Slow 4G"}.get(prof, prof)
    table(f"First load ({label}): total bytes / JS", [(p, (lambda p: lambda n: f'{agg(n, lambda r: b(r, p)["kb"], fmt="{:.1f}", spread=False)} KB / {agg(n, lambda r: b(r, p)["jsKb"], fmt="{:.1f}", spread=False)} KB')(p)) for p in pages])
    table(f"First load ({label}): TTFB / FCP / LCP", [(p, (lambda p: lambda n: f'{agg(n, lambda r: b(r, p)["ttfb"])} / {agg(n, lambda r: b(r, p)["fcp"])} / {agg(n, lambda r: b(r, p)["lcp"], unit=" ms")}')(p)) for p in pages],
          "Core Web Vitals: LCP is good at 2.5 s or less.")
    table(f"First load ({label}): total blocking time / CLS / JS heap", [(p, (lambda p: lambda n: f'{agg(n, lambda r: b(r, p)["tbt"], unit=" ms", spread=False)} / {agg(n, lambda r: b(r, p)["cls"], fmt="{:.3f}", spread=False)} / {agg(n, lambda r: b(r, p)["heapMb"], fmt="{:.1f}", unit=" MB", spread=False)}')(p)) for p in pages],
          "Core Web Vitals: CLS is good at 0.1 or less.")
    i = lambda r, k, prof=prof: r["browser"][prof][k]
    table(f"Interaction ({label})", [
        ("sidebar navigation (click → new content)", lambda n: agg(n, lambda r: i(r, "navigation")["ms"], unit=" ms")),
        ("bytes per navigation", lambda n: agg(n, lambda r: i(r, "navigation")["kb"], fmt="{:.1f}", unit=" KB", spread=False)),
        ("INP over the navigations", lambda n: agg(n, lambda r: i(r, "navigation")["inp"], unit=" ms")),
        ("open the avatar menu (click → visible)", lambda n: agg(n, lambda r: i(r, "menu")["ms"], unit=" ms")),
        ("INP over the menu clicks", lambda n: agg(n, lambda r: i(r, "menu")["inp"], unit=" ms")),
        ("save the issue form (click → \"Changes saved.\")", lambda n: agg(n, lambda r: i(r, "save")["ms"], unit=" ms")),
        ("INP of the save click", lambda n: agg(n, lambda r: i(r, "save")["inp"], unit=" ms")),
    ], "Core Web Vitals: INP is good at 200 ms or less. Event Timing reports interactions of 16 ms or more, so 0 means "
       "every interaction took less than 16 ms. Navigation is measured on desktop only (the sidebar is a drawer on mobile). "
       "The save goes to the server: its time includes the round trip, its INP only the click's own frame.")

print("\n### Runs\n")
print("| Variant | Run | 1-min load at start → end |")
print("|---|---|---|")
for r in rows:
    print(f'| {r["name"]} | {r.get("iteration", 1)} | {r.get("loadStart", "?").split()[0]} → {r.get("loadEnd", "?").split()[0]} |')
