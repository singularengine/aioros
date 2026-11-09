#!/usr/bin/env python3
# Fails when the SDK pinned in global.json lags a security release of its .NET channel, or a package the solution
# restores has a known advisory; warns when a newer non-security release exists.
import json
import subprocess
import sys
import urllib.request
from pathlib import Path

root = Path(__file__).resolve().parent.parent
sdk = json.loads((root / "global.json").read_text())["sdk"]["version"]
channel = ".".join(sdk.split(".")[:2])
url = f"https://builds.dotnet.microsoft.com/dotnet/release-metadata/{channel}/releases.json"
with urllib.request.urlopen(url, timeout=60) as response:
    releases = json.load(response)["releases"]


def version(text):
    return tuple(int(part) for part in text.split("-")[0].split("."))


failures = []
warnings = []
pinned = next((r for r in releases if any(s["version"] == sdk for s in r.get("sdks", []))), None)
if pinned is None:
    failures.append(f"global.json pins SDK {sdk}, which no {channel} release lists")
else:
    band = sdk.split(".")[2][0]
    later = [r for r in releases if version(r["release-version"]) > version(pinned["release-version"])]
    missed = [r for r in later if r["security"]]
    if later:
        newest = max(later, key=lambda r: version(r["release-version"]))
        target = next((s["version"] for s in newest["sdks"] if s["version"].split(".")[2][0] == band), newest["sdk"]["version"])
        cves = sorted({c["cve-id"] for r in missed for c in r.get("cve-list", [])})
        message = f"global.json pins SDK {sdk} (runtime {pinned['release-version']}); the latest is runtime {newest['release-version']}, SDK {target}"
        if missed:
            failures.append(f"{message}, after {len(missed)} security release(s): {', '.join(cves) or 'see the release notes'}")
        else:
            warnings.append(message)

listing = subprocess.run(
    ["dotnet", "list", str(next(root.glob("*.slnx"))), "package", "--vulnerable", "--include-transitive", "--format", "json"],
    capture_output=True, text=True, check=True, cwd=root)
for project in json.loads(listing.stdout)["projects"]:
    for framework in project.get("frameworks", []):
        for package in framework.get("topLevelPackages", []) + framework.get("transitivePackages", []):
            for advisory in package.get("vulnerabilities", []):
                failures.append(f"{Path(project['path']).name}: {package['id']} {package['resolvedVersion']} ({advisory['severity']}, {advisory['advisoryurl']})")

for warning in warnings:
    print(f"::warning::{warning}")
for failure in failures:
    print(f"::error::{failure}")
if not failures:
    print(f"SDK {sdk} carries the latest security release of .NET {channel}; no package has a known advisory")
sys.exit(1 if failures else 0)
