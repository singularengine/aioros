"""Compatibility bench: every route of an Aioros app rendered by Avalonia (Skia, real layout) and by Chromium from
Aioros's HTML, in each theme and interaction state, compared with a threshold per kind of difference.

Per variant (route x theme x state) it measures:
- texts: the share of Avalonia's visible texts that the browser also shows (matched by text, nearest first once the
  shift around each text is taken out: the median shift of its ANCHORS nearest anchors, the texts each render shows
  once, so a page drawn lower or to the side, or one whose sections drift apart, is not matched row against row);
- layout: the share of matched texts whose box (left, top, width, height of the glyph lines, from Avalonia's text
  layout and the browser's client rects) is equal within LAYOUT_PX pixels;
- colors: the share of matched texts whose background (the most frequent color in the box) is equal within
  BACKGROUND_TOLERANCE per channel and whose ink (the color farthest from it, averaged over three horizontal pixels
  so subpixel antialiasing's colored fringes cancel out) within INK_TOLERANCE;
- pixels: the share of content pixels (not the page background in either render) that match. In glyph regions a
  pixel's luminance, averaged over three horizontal pixels, matches another within GLYPH_TOLERANCE up to GLYPH_SHIFT
  pixels away, since the engines antialias text differently (Avalonia's Skia with subpixel antialiasing, headless
  Chromium in grayscale); elsewhere within PIXEL_TOLERANCE up to PIXEL_SHIFT pixel away, so boxes, sizes and colors count.

A baseline (baselines/<app>.json) holds every variant's numbers and the allowance per metric, in percentage points:
ALLOWANCE by default, and per route where a render is not deterministic (a reason goes with it). With --check, a
metric below its baseline by more than its allowance fails the run (exit 1); --update writes the measured numbers as
the new baseline, keeping the allowances. Every run also writes measured.json in the output directory, in the
baseline's format, so a baseline can be taken from a CI run.

With --report <dir>, the run also writes the parity report an app shows (the Gallery's /parity pages): parity.json with
every variant's numbers per route, and per route the light theme's normal state as three PNGs, Avalonia's capture, the
browser's and the differing pixels marked on the browser's (<slug>.avalonia.png, <slug>.web.png, <slug>.diff.png).

Usage: python3 compat.py <base-url> <out-dir> [--routes / /a ...] [--themes light dark] [--states normal ...]
                         [--baseline bench/compat/baselines/gallery.json] [--check | --update] [--report dir]
The app must run in development (it serves /_aioros/capture). Chromium comes from CHROMIUM (default
/usr/bin/chromium), driven by browser.mjs with Node 22 or later; COMPAT_VARIANT_TIMEOUT (seconds, default 60) bounds
each browser capture, retried once in a new tab. The run prints how long each phase took (browser captures, Avalonia
captures, comparison).
"""
import argparse, base64, io, json, os, subprocess, sys, time, urllib.parse, urllib.request
from datetime import date
from pathlib import Path

import numpy as np
from PIL import Image

WIDTH, HEIGHT = 1280, 900
LAYOUT_PX = 1.0
ANCHORS = 5
BACKGROUND_TOLERANCE, INK_TOLERANCE = 3, 48
PIXEL_TOLERANCE, PIXEL_SHIFT = 8, 1
GLYPH_TOLERANCE, GLYPH_SHIFT = 40, 2
ALLOWANCE = {"texts": 1.0, "layout": 1.0, "colors": 1.0, "pixels": 0.5}
METRICS = ("texts", "layout", "colors", "pixels")
HERE = Path(__file__).resolve().parent


def avalonia(base, route, theme, state):
    query = urllib.parse.urlencode({"path": route, "theme": theme, "state": state, "width": WIDTH, "height": HEIGHT, "format": "json"})
    with urllib.request.urlopen(f"{base}/_aioros/capture?{query}", timeout=120) as response:
        captured = json.load(response)
    image = Image.open(io.BytesIO(base64.b64decode(captured["png"]))).convert("RGB")
    return image, captured["texts"]


def browser(base, out, variants):
    jobs = out / "variants.json"
    jobs.write_text(json.dumps(variants))
    limit = int(os.environ.get("COMPAT_VARIANT_TIMEOUT", "60"))
    subprocess.run(["node", str(HERE / "browser.mjs"), base, str(out), str(WIDTH), str(HEIGHT), str(jobs)], check=True, timeout=60 + 2 * limit * len(variants))


def normalized(text):
    return " ".join(text.split()).casefold()


def visible(texts):
    return [t for t in texts if t["width"] > 0 and t["height"] > 0 and t["x"] < WIDTH and t["y"] < HEIGHT and t["x"] + t["width"] > 0 and t["y"] + t["height"] > 0]


def anchors(ours, theirs):
    """The texts each render shows once, paired: the anchors whose shifts say where the web drew Avalonia's texts."""
    counts = lambda texts: {key: [normalized(t["text"]) for t in texts].count(key) for key in {normalized(t["text"]) for t in texts}}
    mine, yours = counts(ours), counts(theirs)
    first = lambda texts, key: next(t for t in texts if normalized(t["text"]) == key)
    return [(first(ours, key), first(theirs, key)) for key in mine if mine[key] == 1 and yours.get(key) == 1]


def offset(a, pairs, nearest=ANCHORS):
    """The shift of the web's texts around Avalonia's text a: the median shift of its nearest anchors, so a page drawn
    lower or to the side, or one whose rows drift apart, is matched text by text rather than row against row."""
    if not pairs:
        return 0.0, 0.0
    close = sorted(pairs, key=lambda p: abs(p[0]["x"] - a["x"]) + abs(p[0]["y"] - a["y"]))[:nearest]
    return float(np.median([b["x"] - p["x"] for p, b in close])), float(np.median([b["y"] - p["y"] for p, b in close]))


def match(ours, theirs):
    pairs, found = [], anchors(ours, theirs)
    for i, a in enumerate(ours):
        dx = dy = None
        for j, b in enumerate(theirs):
            if normalized(a["text"]) == normalized(b["text"]):
                if dx is None:
                    dx, dy = offset(a, found)
                distance = abs(a["x"] + dx - b["x"]) + abs(a["y"] + dy - b["y"])
                pairs.append((distance, i, j))
    used_a, used_b, matched = set(), set(), []
    for _, i, j in sorted(pairs):
        if i not in used_a and j not in used_b:
            used_a.add(i), used_b.add(j)
            matched.append((ours[i], theirs[j]))
    return matched


def box(image, text):
    x0, y0 = max(0, int(text["x"])), max(0, int(text["y"]))
    x1, y1 = min(WIDTH, int(np.ceil(text["x"] + text["width"]))), min(HEIGHT, int(np.ceil(text["y"] + text["height"])))
    return image[y0:y1, x0:x1].reshape(-1, 3) if x1 > x0 and y1 > y0 else None


def mode(pixels):
    packed = (pixels[:, 0].astype(np.int64) << 16) | (pixels[:, 1].astype(np.int64) << 8) | pixels[:, 2].astype(np.int64)
    values, counts = np.unique(packed, return_counts=True)
    top = int(values[counts.argmax()])
    return np.array([(top >> 16) & 255, (top >> 8) & 255, top & 255], dtype=np.int16)


def smoothed(image):
    padded = np.pad(image.astype(np.float32), ((0, 0), (1, 1), (0, 0)), mode="edge")
    return (padded[:, :-2] + padded[:, 1:-1] + padded[:, 2:]) / 3


def colors(image, text):
    pixels = box(image, text)
    if pixels is None or len(pixels) == 0:
        return None
    background = mode(pixels)
    ink = box(smoothed(image), text)
    return background, ink[np.abs(ink - background).sum(axis=1).argmax()]


def same_colors(a_image, b_image, a, b):
    ours, theirs = colors(a_image, a), colors(b_image, b)
    if ours is None or theirs is None:
        return False
    return np.abs(ours[0] - theirs[0]).max() <= BACKGROUND_TOLERANCE and np.abs(ours[1] - theirs[1]).max() <= INK_TOLERANCE


def glyphs(texts):
    mask = np.zeros((HEIGHT, WIDTH), dtype=bool)
    for t in texts:
        x0, y0 = max(0, int(t["x"]) - 2), max(0, int(t["y"]) - 2)
        x1, y1 = min(WIDTH, int(np.ceil(t["x"] + t["width"])) + 2), min(HEIGHT, int(np.ceil(t["y"] + t["height"])) + 2)
        mask[y0:y1, x0:x1] = True
    return mask


def differing(a, b, tolerance, shift):
    differs = np.ones(a.shape[:2], dtype=bool)
    spread = ((shift, shift), (shift, shift)) + ((0, 0),) * (a.ndim - 2)
    padded = np.pad(b, spread, mode="edge")
    for dy in range(-shift, shift + 1):
        for dx in range(-shift, shift + 1):
            moved = padded[shift + dy:shift + dy + HEIGHT, shift + dx:shift + dx + WIDTH]
            distance = np.abs(a - moved)
            differs &= (distance.max(axis=2) if a.ndim == 3 else distance) > tolerance
    return differs


def luminance(image):
    return smoothed(image) @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)


def compare(ava, web, ava_texts, web_texts):
    a = np.asarray(ava.resize((WIDTH, HEIGHT)), dtype=np.int16)
    b = np.asarray(web.resize((WIDTH, HEIGHT)), dtype=np.int16)
    ours, theirs = visible(ava_texts), visible(web_texts)
    matched = match(ours, theirs)
    exact = [(x, y) for x, y in matched if all(abs(x[k] - y[k]) <= LAYOUT_PX for k in ("x", "y", "width", "height"))]
    painted = [(x, y) for x, y in matched if same_colors(a, b, x, y)]
    in_glyphs = glyphs(ours + theirs)
    differs = np.where(in_glyphs, differing(luminance(a), luminance(b), GLYPH_TOLERANCE, GLYPH_SHIFT), differing(a, b, PIXEL_TOLERANCE, PIXEL_SHIFT))
    paper = mode(a.reshape(-1, 3))
    ink = (np.abs(a - paper).max(axis=2) > PIXEL_TOLERANCE) | (np.abs(b - paper).max(axis=2) > PIXEL_TOLERANCE)
    share = lambda part, whole: round(100 * part / whole, 2) if whole else 100.0
    offsets = [abs(x["y"] - y["y"]) for x, y in matched]
    metrics = {
        "texts": share(len(matched), len(ours)),
        "layout": share(len(exact), len(matched)),
        "colors": share(len(painted), len(matched)),
        "pixels": share(int((~differs & ink).sum()), int(ink.sum())),
        "count": {"avalonia": len(ours), "web": len(theirs), "matched": len(matched)},
        "median_dy": round(float(np.median(offsets)), 2) if offsets else 0.0,
    }
    return metrics, differs, [(x, y) for x, y in matched if (x, y) not in exact], [t for t in ours if all(t is not x for x, _ in matched)]


def name_of(route, theme, state):
    return f"{route.strip('/').replace('/', '_') or 'home'}.{theme}.{state}"


def key_of(route, theme, state):
    return f"{route} {theme} {state}"


def report(rows, failures, out):
    lines = ["| Route | Theme | State | Texts | Layout | Colors | Pixels | Median dy |", "| --- | --- | --- | --- | --- | --- | --- | --- |"]
    lines += [f"| `{r['route']}` | {r['theme']} | {r['state']} | {r['texts']}% | {r['layout']}% | {r['colors']}% | {r['pixels']}% | {r['median_dy']} px |" for r in rows]
    if failures:
        lines += ["", "Regressions beyond the threshold:", ""] + [f"- {failure}" for failure in failures]
    (out / "COMPAT.md").write_text("\n".join(lines) + "\n")
    (out / "compat.json").write_text(json.dumps(rows, indent=2))


def slug_of(route):
    return route.strip("/").replace("/", "_") or "home"


def publish(rows, out, target):
    target.mkdir(parents=True, exist_ok=True)
    routes = list(dict.fromkeys(r["route"] for r in rows))
    for route in routes:
        source = name_of(route, "light", "normal")
        for kind in ("avalonia", "web", "diff"):
            image = out / f"{source}.{kind}.png"
            if image.exists():
                Image.open(image).convert("RGB").quantize(colors=256, method=Image.Quantize.MEDIANCUT).save(target / f"{slug_of(route)}.{kind}.png", optimize=True)
    families = [{"route": route, "slug": slug_of(route), "variants": [{k: r[k] for k in ("theme", "state", *METRICS)} for r in rows if r["route"] == route]} for route in routes]
    data = {"measured": date.today().isoformat(), "width": WIDTH, "height": HEIGHT, "shown": "light normal", "routes": families}
    (target / "parity.json").write_text(json.dumps(data, indent=1) + "\n")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("base")
    parser.add_argument("out", type=Path)
    parser.add_argument("--routes", nargs="+", default=["/"])
    parser.add_argument("--themes", nargs="+", default=["light", "dark"])
    parser.add_argument("--states", nargs="+", default=["normal", "pointerover", "pressed", "focus"])
    parser.add_argument("--baseline", type=Path, default=HERE / "baselines" / "gallery.json")
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--check", action="store_true")
    mode.add_argument("--update", action="store_true")
    parser.add_argument("--report", type=Path)
    args = parser.parse_args()
    base = args.base.rstrip("/")
    args.out.mkdir(parents=True, exist_ok=True)
    variants = [{"name": name_of(r, t, s), "route": r, "theme": t, "state": s} for r in args.routes for t in args.themes for s in args.states]
    started = time.monotonic()
    browser(base, args.out, variants)
    phases = {"browser captures": time.monotonic() - started, "Avalonia captures": 0.0, "comparison": 0.0}
    baseline = json.loads(args.baseline.read_text()) if args.baseline.exists() else {}
    rows, failures, measured = [], [], {}
    for v in variants:
        started = time.monotonic()
        ava, ava_texts = avalonia(base, v["route"], v["theme"], v["state"])
        phases["Avalonia captures"] += time.monotonic() - started
        started = time.monotonic()
        ava.save(args.out / f"{v['name']}.avalonia.png")
        (args.out / f"{v['name']}.avalonia.json").write_text(json.dumps(ava_texts))
        web = Image.open(args.out / f"{v['name']}.web.png").convert("RGB")
        web_texts = json.loads((args.out / f"{v['name']}.web.json").read_text())
        metrics, differs, misplaced, missing = compare(ava, web, ava_texts, web_texts)
        overlay = np.asarray(web.resize((WIDTH, HEIGHT))).copy()
        overlay[differs] = [255, 0, 80]
        Image.fromarray(overlay).save(args.out / f"{v['name']}.diff.png")
        (args.out / f"{v['name']}.texts.json").write_text(json.dumps({"misplaced": misplaced, "missing": missing}, indent=1))
        row = {"route": v["route"], "theme": v["theme"], "state": v["state"], **metrics}
        rows.append(row)
        phases["comparison"] += time.monotonic() - started
        key = key_of(v["route"], v["theme"], v["state"])
        measured[key] = {m: metrics[m] for m in METRICS}
        expected = baseline.get("variants", {}).get(key)
        allowed = {**ALLOWANCE, **baseline.get("allowance", {}), **baseline.get("routes", {}).get(v["route"], {}).get("allowance", {})}
        for metric in METRICS:
            if expected is not None and metrics[metric] < expected[metric] - allowed[metric]:
                failures.append(f"{key}: {metric} {metrics[metric]}% < baseline {expected[metric]}% - {allowed[metric]}")
        if args.check and expected is None:
            failures.append(f"{key}: no baseline")
        print(f"{key}: texts {metrics['texts']}%, layout {metrics['layout']}%, colors {metrics['colors']}%, pixels {metrics['pixels']}%, median dy {metrics['median_dy']} px", flush=True)
    print(f"{len(variants)} variants: " + ", ".join(f"{name} {seconds:.0f} s" for name, seconds in phases.items()), flush=True)
    report(rows, failures, args.out)
    if args.report:
        publish(rows, args.out, args.report)
    recorded = {"allowance": baseline.get("allowance", ALLOWANCE), "routes": baseline.get("routes", {}), "variants": {**baseline.get("variants", {}), **measured}}
    (args.out / "measured.json").write_text(json.dumps(recorded, indent=2, sort_keys=True) + "\n")
    if args.update:
        args.baseline.write_text(json.dumps(recorded, indent=2, sort_keys=True) + "\n")
        print(f"baseline written to {args.baseline}")
    if args.check and failures:
        print("\n".join(["Compatibility regressions:"] + failures), file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
