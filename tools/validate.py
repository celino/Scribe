#!/usr/bin/env python3
"""Fails early: tests/FEATURES.md matches the README and the unit tests; .version is valid JSON and complete, README has no leftover placeholders, the CHANGELOG has a section
for the .version's version (and for the tag, with --tag), and the code agrees with the version.

Usage: python3 tools/validate.py [--tag vX.Y.Z]
"""
import json
import re
import sys

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
for pat in (r"\{\{", r"\[conferir\]"):
    if re.search(pat, readme):
        problems.append(f"README.md still has {pat}")

# feature matrix: tests/FEATURES.md <-> README <-> unit tests
import glob
matrix = open("tests/FEATURES.md", encoding="utf-8").read()
test_src = "".join(open(f, encoding="utf-8").read() for f in glob.glob("tests/Scribe.Tests/*.cs"))
rows = re.findall(r"^\| (F\d+) \| (.+?) \| (.+?) \| (.+?) \| (planned|automated) \| (.+?) \|$", matrix, re.M)
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
        if not re.search(rf"\b{re.escape(prefix)}\w*\(", test_src):
            problems.append(f"{fid}: automated, but no test method starts with {prefix}")
        if f'Trait("Feature", "{fid}")' not in test_src:
            problems.append(f"{fid}: automated, but no test has Trait Feature {fid}")
for used in set(re.findall(r'Trait\("Feature", "(F\d+)"\)', test_src)):
    if used not in ids:
        problems.append(f"{used}: used by a test but missing from tests/FEATURES.md")

if "--tag" in sys.argv:
    tag = sys.argv[sys.argv.index("--tag") + 1].lstrip("v")
    if tag != version:
        problems.append(f"tag {tag} differs from .version {version}")

if problems:
    sys.exit("\n".join(problems))
print(f"ok: Scribe {version}")
