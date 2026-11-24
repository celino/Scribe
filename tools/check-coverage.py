#!/usr/bin/env python3
"""Fails when the unit-test coverage of the core is below a floor.

    python3 tools/check-coverage.py [--lines 90] [--branches 80] [folder]

Reads the newest coverage.cobertura.xml under the folder (default tests/TestResults), which `dotnet test` writes when it
runs with tests/coverlet.runsettings. Only Source/Core is measured; the KSP module needs the game (see docs/TESTING.md).
"""
import argparse
import glob
import os
import sys
import xml.etree.ElementTree as ET


def newest_report(folder):
    files = glob.glob(os.path.join(folder, "**", "coverage.cobertura.xml"), recursive=True)
    return max(files, key=os.path.getmtime) if files else None


def rates(path):
    root = ET.parse(path).getroot()
    return float(root.get("line-rate", "0")) * 100, float(root.get("branch-rate", "0")) * 100


def main(argv):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("folder", nargs="?", default="tests/TestResults")
    ap.add_argument("--lines", type=float, default=90.0, help="minimum line coverage in percent")
    ap.add_argument("--branches", type=float, default=80.0, help="minimum branch coverage in percent")
    a = ap.parse_args(argv)
    report = newest_report(a.folder)
    if report is None:
        print("no coverage.cobertura.xml under " + a.folder + " (run dotnet test with --settings tests/coverlet.runsettings)")
        return 2
    lines, branches = rates(report)
    print("core coverage: lines %.1f %% (floor %.0f), branches %.1f %% (floor %.0f)" % (lines, a.lines, branches, a.branches))
    ok = lines >= a.lines and branches >= a.branches
    if not ok:
        print("FAIL: coverage is below the floor")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
