#!/usr/bin/env python3
"""Turns one in-game round into PASS/FAIL.

    python3 -I tests/ingame/check_round.py KSP.log --expect F01,F07 [--allow-missing F10]

Fails when: an ASSERT line says FAIL; an expected feature id has no ASSERT line; the driver never
printed "done"; the log shows the Scribe assemblies failing to load or an exception that mentions Scribe.
Prints one line per check, then the verdict. Exit status 0 = pass, 1 = fail.
"""
import argparse
import re
import sys

ASSERT = re.compile(r"\[ScribeRound\] ASSERT (F\d+) (PASS|FAIL) ?(.*)")
BAD_LOAD = re.compile(r"Load failed due missing dependencies|Could not load (file or )?assembly.*Scribe", re.I)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("log")
    ap.add_argument("--expect", default="", help="comma-separated feature ids that must have an ASSERT")
    ap.add_argument("--allow-missing", default="", help="ids whose ASSERT may be absent (the rest still counts)")
    a = ap.parse_args()
    expect = [x for x in a.expect.split(",") if x]
    optional = {x for x in a.allow_missing.split(",") if x}

    results, done, problems = [], False, []
    prev = ""
    with open(a.log, errors="replace") as f:
        for line in f:
            m = ASSERT.search(line)
            if m:
                results.append((m.group(1), m.group(2), m.group(3).strip()))
            if "[ScribeRound] done" in line:
                done = True
            if BAD_LOAD.search(line):
                problems.append("assembly load problem: " + line.strip()[:160])
            if "Exception" in prev and re.search(r"\bScribe(\.|Module|Core)", line):
                problems.append("exception in Scribe: " + prev.strip()[:100] + " | " + line.strip()[:100])
            prev = line

    ok = True
    for fid, verdict, detail in results:
        print(f"ASSERT {fid} {verdict} {detail}")
        ok &= verdict == "PASS"
    seen = {r[0] for r in results}
    for fid in expect:
        if fid not in seen and fid not in optional:
            print(f"MISSING {fid}: no ASSERT line in the log")
            ok = False
    if not done:
        print("MISSING done: the driver never finished (game crashed or stuck)")
        ok = False
    for p in problems:
        print("PROBLEM " + p)
        ok = False
    print("RESULT " + ("PASS" if ok else "FAIL"))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
