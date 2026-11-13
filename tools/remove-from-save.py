#!/usr/bin/env python3
"""Remove the Scribe ledger from a KSP save (13/11/2026).

KSP keeps the SCENARIO node of a removed mod and writes it back on every save; the game ignores it, but it stays
in the file. This script deletes the `SCENARIO { name = ScribeModule ... }` block from the given .sfs files.

  remove-from-save.py <save folder or .sfs> [...]      dry run: says what it would remove
  remove-from-save.py --apply <save folder or .sfs> [...]   writes <file>.bak-scribe first, then removes

Only that block is touched; nothing else in the file changes. Close KSP first.
"""
import shutil, sys
from pathlib import Path


def find_block(text):
    """(start, end) of the SCENARIO block whose name is ScribeModule, or None. Brace matching on the raw text."""
    pos = 0
    while True:
        i = text.find("SCENARIO", pos)
        if i < 0:
            return None
        pos = i + 8
        if text[i - 1:i] not in ("\n", "\t", " ", ""):
            continue
        j = text.find("{", i)
        if j < 0 or text[i + 8:j].strip():
            continue
        depth, k = 0, j
        while k < len(text):
            c = text[k]
            if c == "{":
                depth += 1
            elif c == "}":
                depth -= 1
                if depth == 0:
                    break
            k += 1
        body = text[j:k + 1]
        first = [l.strip() for l in body.splitlines() if l.strip() and l.strip() not in ("{",)][:1]
        if any(l.replace(" ", "") == "name=ScribeModule" for l in body.splitlines()[:4]) or (first and first[0].replace(" ", "") == "name=ScribeModule"):
            start = text.rfind("\n", 0, i) + 1
            end = k + 1
            if text[end:end + 1] == "\n":
                end += 1
            return start, end


def main(argv):
    apply = "--apply" in argv
    paths = [Path(a) for a in argv if not a.startswith("--")]
    if not paths:
        print(__doc__)
        return 2
    files = []
    for p in paths:
        files += sorted(p.glob("*.sfs")) if p.is_dir() else [p]
    total = 0
    for f in files:
        text = f.read_text(encoding="utf-8", errors="surrogateescape")
        b = find_block(text)
        if not b:
            continue
        n = text[b[0]:b[1]].count("ENTRY")
        total += 1
        print(f"{f}: Scribe block, {b[1]-b[0]} bytes, {n} entries" + ("" if apply else "  (dry run)"))
        if apply:
            shutil.copy2(f, str(f) + ".bak-scribe")
            f.write_text(text[:b[0]] + text[b[1]:], encoding="utf-8", errors="surrogateescape")
    print(f"{total} file(s) with a Scribe block out of {len(files)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
