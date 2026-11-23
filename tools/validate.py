#!/usr/bin/env python3
"""Fails early: tests/FEATURES.md matches the README and the unit tests; .version is valid JSON and complete, README has no leftover placeholders, the CHANGELOG has a section
for the .version's version (and for the tag, with --tag), and the code agrees with the version.

Usage: python3 tools/validate.py [--tag vX.Y.Z]
"""
import json
import re
import sys
import os

problems = []
ver_path = "GameData/Scribe/Scribe.version"
try:
    data = json.load(open(ver_path, encoding="utf-8"))
except Exception as e:
    sys.exit(f"{ver_path}: {e}")
for key in ("NAME", "VERSION", "KSP_VERSION", "KSP_VERSION_MIN", "KSP_VERSION_MAX"):
    if key not in data:
        problems.append(f"{ver_path}: missing {key}")
v = data.get("VERSION", {})
version = "%d.%d.%d" % (v.get("MAJOR", 0), v.get("MINOR", 0), v.get("PATCH", 0))

changelog = open("CHANGELOG.md", encoding="utf-8").read()
if not re.search(rf"^## {re.escape(version)}\b", changelog, re.M):
    problems.append(f"CHANGELOG.md has no section for {version}")
if "Flown in a career save so far" not in changelog:
    problems.append("CHANGELOG.md has no 'Flown in a career save so far' line")

readme = open("README.md", encoding="utf-8").read()
for pat in (r"\{\{", r"\[conferir\]", r"\[RELEASE LINK\]"):
    if re.search(pat, readme):
        problems.append(f"README.md still has {pat}")

# feature matrix: tests/FEATURES.md <-> README <-> unit tests
import glob
matrix = open("tests/FEATURES.md", encoding="utf-8").read()
driver_src = "".join(open(f, encoding="utf-8").read() for f in glob.glob("tests/ingame/driver*/*.cs"))
test_src = "".join(open(f, encoding="utf-8").read() for f in glob.glob("tests/Scribe.Tests/*.cs"))
py_src = "".join(open(f, encoding="utf-8").read() for f in glob.glob("tests/test_*.py"))
rows = re.findall(r"^\| (F\d+) \| (.+?) \| (.+?) \| (.+?) \| (planned|automated|ingame) \| (.+?) \|$", matrix, re.M)
if not rows:
    problems.append("tests/FEATURES.md: no feature rows found")
ids = [r[0] for r in rows]
if len(ids) != len(set(ids)):
    problems.append("tests/FEATURES.md: duplicate feature ID")
readme_flat = " ".join(readme.split()).lower()
for fid, _name, phrase, _layer, status, test in rows:
    if phrase.strip().lower() not in readme_flat:
        problems.append(f"{fid}: README phrase not found: {phrase!r}")
    if status == "automated":
        prefix = test.strip("`")
        if prefix.startswith("test_"):
            if not re.search(rf"def {re.escape(prefix)}\w*\(", py_src):
                problems.append(f"{fid}: automated, but no Python test starts with {prefix}")
            continue
        if not re.search(rf"\b{re.escape(prefix)}\w*\(", test_src):
            problems.append(f"{fid}: automated, but no test method starts with {prefix}")
        if f'Trait("Feature", "{fid}")' not in test_src:
            problems.append(f"{fid}: automated, but no test has Trait Feature {fid}")
    if status == "ingame" and f".Assert(\"{fid}\"" not in driver_src:
        problems.append(f'{fid}: ingame, but the round driver has no Assert("{fid}", ...)')
for used in set(re.findall(r'Trait\("Feature", "(F\d+)"\)', test_src)):
    if used not in ids:
        problems.append(f"{used}: used by a test but missing from tests/FEATURES.md")

# language: what ships in the repo is English (the brand footer in README.md is the only exception)
ACCENTED = re.compile("[\u00e1\u00e9\u00ed\u00f3\u00fa\u00e2\u00ea\u00f4\u00e3\u00f5\u00e7\u00c1\u00c9\u00cd\u00d3\u00da\u00c7]")
for folder in ("Source", "tests", "tools"):
    for path in glob.glob(folder + "/**/*", recursive=True):
        if not re.search(r"\.(cs|py|sh|json|cfg|md|csproj|yml)$", path) or re.search(r"/(obj|out|bin|TestResults)/", path):
            continue
        for n, line in enumerate(open(path, encoding="utf-8", errors="replace"), 1):
            if ACCENTED.search(line):
                problems.append(f"{path}:{n}: accented letters (the repo is English only)")
                break

# language: common Portuguese words without accents (the accent check alone misses them)
PT = re.compile(r"\b(nao|voce|entao|porque|arquivo|pasta|rodada|nave|jogo|teste de|para o|para a|esta sem)\b", re.I)
for path in glob.glob("**/*", recursive=True):
    if not re.search(r"\.(cs|py|sh|json|md|yml|csproj|netkan|version)$", path) or re.search(r"(^|/)(obj|out|bin|TestResults|ksp-test|dist)/|^tools/validate\.py$", path):
        continue
    for n, line in enumerate(open(path, encoding="utf-8", errors="replace"), 1):
        if PT.search(line):
            problems.append(f"{path}:{n}: looks Portuguese (the repo is English only)")
            break

# docs: community files exist, every ADR is in the index, relative links in the docs resolve, no accented letters
for needed in ("CONTRIBUTING.md", "SECURITY.md", "CODE_OF_CONDUCT.md", "docs/adr/README.md"):
    if not os.path.exists(needed):
        problems.append(f"{needed}: missing")
if os.path.exists("docs/adr/README.md"):
    index = open("docs/adr/README.md", encoding="utf-8").read()
    for path in sorted(glob.glob("docs/adr/[0-9]*.md")):
        if os.path.basename(path) not in index:
            problems.append(f"{path}: not listed in docs/adr/README.md")
        if not re.search(r"^Status: (Accepted|Superseded|Proposed)", open(path, encoding="utf-8").read(), re.M):
            problems.append(f"{path}: no 'Status:' line")
for path in glob.glob("*.md") + glob.glob("docs/**/*.md", recursive=True):
    text = open(path, encoding="utf-8").read()
    if ACCENTED.search(text):
        problems.append(f"{path}: accented letters (the repo is English only)")
    for target in re.findall(r"\]\(([^)#\s]+)(?:#[^)]*)?\)", text):
        if re.match(r"[a-z]+:", target):
            continue
        if not os.path.exists(os.path.normpath(os.path.join(os.path.dirname(path), target))):
            problems.append(f"{path}: broken link {target}")

if "--tag" in sys.argv:
    tag = sys.argv[sys.argv.index("--tag") + 1].lstrip("v")
    if tag != version:
        problems.append(f"tag {tag} differs from .version {version}")

if problems:
    sys.exit("\n".join(problems))
print(f"ok: Scribe {version}")
