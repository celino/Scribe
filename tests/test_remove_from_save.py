"""F15: tools/remove-from-save.py takes the ledger out of a save and keeps a backup.

Run with: python3 -I -m unittest discover -s tests -p 'test_*.py'
"""
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SCRIPT = ROOT / "tools" / "remove-from-save.py"
SAVE = ROOT / "tests" / "ingame" / "save" / "persistent.sfs"
LEDGER = ROOT / "tests" / "ingame" / "ledger-node.cfg"


def run(*args):
    return subprocess.run([sys.executable, "-I", str(SCRIPT), *map(str, args)], capture_output=True, text=True)


def with_ledger():
    s = SAVE.read_text()
    i = s.index("\tSCENARIO")
    return s, s[:i] + LEDGER.read_text() + s[i:]


class RemoveFromSave(unittest.TestCase):
    def setUp(self):
        self.tmp = Path(tempfile.mkdtemp())
        self.clean, dirty = with_ledger()
        self.sfs = self.tmp / "persistent.sfs"
        self.sfs.write_text(dirty)
        self.dirty = dirty

    def tearDown(self):
        shutil.rmtree(self.tmp)

    def test_F15_dry_run_changes_nothing(self):
        r = run(self.sfs)
        self.assertEqual(r.returncode, 0, r.stderr)
        self.assertEqual(self.sfs.read_text(), self.dirty)
        self.assertFalse(list(self.tmp.glob("*.bak-scribe")))

    def test_F15_apply_removes_only_the_ledger_and_keeps_a_backup(self):
        r = run("--apply", self.sfs)
        self.assertEqual(r.returncode, 0, r.stderr)
        self.assertEqual(self.sfs.read_text(), self.clean)
        backups = list(self.tmp.glob("*.bak-scribe"))
        self.assertEqual(len(backups), 1)
        self.assertEqual(backups[0].read_text(), self.dirty)

    def test_F15_second_run_finds_nothing_to_remove(self):
        run("--apply", self.sfs)
        before = self.sfs.read_text()
        r = run("--apply", self.sfs)
        self.assertEqual(r.returncode, 0, r.stderr)
        self.assertEqual(self.sfs.read_text(), before)


if __name__ == "__main__":
    unittest.main()
