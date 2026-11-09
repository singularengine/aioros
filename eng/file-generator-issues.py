#!/usr/bin/env python3
# Files an issue for each bug or crash the page generators found (the findings files their tests write with FUZZ_OUT
# and META_OUT), one per smallest failing page: an issue whose fingerprint is already open is left alone, and a page
# whose issue was closed is filed again as a regression of it.
import hashlib
import json
import os
import sys
import urllib.parse
import urllib.request
from pathlib import Path

server = os.environ["GITHUB_SERVER_URL"].rstrip("/")
repository = os.environ["GITHUB_REPOSITORY"]
token = os.environ["TOKEN"]
commit = os.environ.get("GITHUB_SHA", "")[:8]
api = f"{server}/api/v1/repos/{repository}"


def request(method, path, body=None):
    data = json.dumps(body).encode() if body is not None else None
    call = urllib.request.Request(f"{api}{path}", data=data, method=method)
    call.add_header("Authorization", f"token {token}")
    call.add_header("Content-Type", "application/json")
    with urllib.request.urlopen(call, timeout=60) as response:
        return json.load(response)


def known(fingerprint):
    query = urllib.parse.urlencode({"state": "all", "type": "issues", "q": fingerprint, "limit": 50})
    return [issue for issue in request("GET", f"/issues?{query}") if fingerprint in issue["title"]]


generators = {"FUZZ_OUT": ("hand grammar", "FUZZ_SEED", "FUZZ_COUNT", "ENGINE-327"), "META_OUT": ("Avalonia's metadata", "META_SEED", "META_COUNT", "ENGINE-331")}
filed = 0
for setting, (name, seed_setting, count_setting, criterion) in generators.items():
    path = Path(os.environ.get(setting, "") + ".json")
    if not os.environ.get(setting) or not path.exists():
        continue
    for finding in json.loads(path.read_text()):
        fingerprint = hashlib.sha256(finding["page"].encode()).hexdigest()[:12]
        issues = known(fingerprint)
        if any(issue["state"] == "open" for issue in issues):
            print(f"{fingerprint}: already open")
            continue
        seed = finding["seeds"][0]
        what = "crashes on" if finding["outcome"] == "crash" else "disagrees with Avalonia on"
        closed = [f"#{issue['number']}" for issue in issues]
        body = "\n".join([
            f"The nightly generator run ({name}, commit {commit}) found a page under the guarantee where Aioros {what} the render.",
            *([f"It is a regression of {', '.join(closed)}."] if closed else []),
            "",
            f"Seeds: {', '.join(str(s) for s in finding['seeds'])}",
            "",
            "Smallest page that still fails:",
            "",
            "```xml",
            finding["page"],
            "```",
            "",
            "Failure:",
            "",
            "```",
            finding.get("failure") or "",
            "```",
            "",
            "Reproduce:",
            "",
            "```",
            f"{seed_setting}={seed} {count_setting}=1 dotnet test --project tests/Aioros.Avalonia.Tests -c Release -- --filter-trait \"AC={criterion}\"",
            "```",
        ])
        issue = request("POST", "/issues", {"title": f"Generated page: Aioros {what} the render ({fingerprint})", "body": body})
        print(f"{fingerprint}: filed #{issue['number']}")
        filed += 1

print(f"{filed} issue(s) filed")
sys.exit(0)
