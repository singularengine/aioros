#!/usr/bin/env python3
# Compares Aioros's closed-loop req/s of a quick-profile run (principle 9, docs/benchmarks.md, "Regression check"). When
# the run measured a base build beside it (variant aioros-base, bench/regression.sh with BASE), each page's change is the
# median over the runs of this tree's req/s divided by the base's in the same run; otherwise it compares the run's
# medians with the stored baseline of the same runner, and a run from another runner (CPU model or server CPUs) is not
# compared. Fails when the geometric mean of the pages' changes drops more than GEO_MARGIN or one page's more than
# PAGE_MARGIN. With --update, writes the run's medians as the new stored baseline instead.
# usage: bench/regression.py <app.jsonl> <env.json> <baseline.json> [--update]
import json, math, os, statistics, sys

GEO_MARGIN = float(os.environ.get("GEO_MARGIN", "0.15"))
PAGE_MARGIN = float(os.environ.get("PAGE_MARGIN", "0.25"))


def runs(path, name):
    pages = {}
    for line in open(path):
        if not line.strip():
            continue
        row = json.loads(line)
        if row["name"] != name:
            continue
        for ssr in row["ssr"]:
            if ssr.get("c64"):
                pages.setdefault(ssr["page"], {})[row["iteration"]] = ssr["c64"]["rps"]
    return pages


def medians(pages):
    return {page: statistics.median(values.values()) for page, values in pages.items()}


def geomean(values):
    return math.exp(sum(math.log(v) for v in values) / len(values))


def check(rows, label):
    print(f"| page | {label} | this run | change |")
    print("|---|---|---|---|")
    failures = []
    for page, before, after, change in rows:
        print(f"| {page} | {before:,.0f} | {after:,.0f} | {change:+.1%} |")
        if change < -PAGE_MARGIN:
            failures.append(f"{page} dropped {-change:.1%} (more than {PAGE_MARGIN:.0%})")
    geo = geomean([1 + change for _, _, _, change in rows]) - 1
    print(f"| geometric mean | | | {geo:+.1%} |")
    if geo < -GEO_MARGIN:
        failures.append(f"the geometric mean dropped {-geo:.1%} (more than {GEO_MARGIN:.0%})")
    for failure in failures:
        print(f"regression: {failure}")
    return 1 if failures else 0


def main():
    jsonl, env_path, baseline_path = sys.argv[1:4]
    env = json.load(open(env_path))
    run = runs(jsonl, "aioros")
    base = runs(jsonl, "aioros-base")
    if base:
        rows = []
        for page in base:
            paired = [run[page][i] / base[page][i] for i in base[page] if i in run.get(page, {})]
            rows.append((page, statistics.median(base[page].values()), statistics.median(run[page].values()), statistics.median(paired) - 1))
        return check(rows, "base, same runs")
    runner = {"cpu": env.get("cpu"), "serverCpus": env.get("serverCpus")}
    if "--update" in sys.argv:
        json.dump({"runner": runner, "commit": env.get("git"), "rps": {p: round(v) for p, v in medians(run).items()}}, open(baseline_path, "w"), indent=1)
        print(f"baseline written: {baseline_path}")
        return 0
    baseline = json.load(open(baseline_path))
    if baseline["runner"] != runner:
        print(f"not compared: the baseline was measured on {baseline['runner']}, this run on {runner}")
        return 0
    measured = medians(run)
    rows = [(page, baseline["rps"][page], measured[page], measured[page] / baseline["rps"][page] - 1) for page in baseline["rps"] if page in measured]
    return check(rows, f"baseline ({baseline['commit']})")


if __name__ == "__main__":
    sys.exit(main())
