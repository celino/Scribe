import importlib.util
import os
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location("check_coverage", os.path.join(HERE, "..", "tools", "check-coverage.py"))
cc = importlib.util.module_from_spec(spec)
spec.loader.exec_module(cc)


def report(folder, line, branch):
    sub = os.path.join(folder, "run1")
    os.makedirs(sub)
    with open(os.path.join(sub, "coverage.cobertura.xml"), "w", encoding="utf-8") as f:
        f.write('<?xml version="1.0"?><coverage line-rate="%s" branch-rate="%s"></coverage>' % (line, branch))


class CoverageFloor(unittest.TestCase):
    def run_main(self, line, branch, *args):
        with tempfile.TemporaryDirectory() as d:
            report(d, line, branch)
            return cc.main([d, *args])

    def test_passes_above_the_floor(self):
        self.assertEqual(0, self.run_main("0.95", "0.85"))

    def test_fails_on_lines_below_the_floor(self):
        self.assertEqual(1, self.run_main("0.89", "0.95"))

    def test_fails_on_branches_below_the_floor(self):
        self.assertEqual(1, self.run_main("0.99", "0.79"))

    def test_the_floor_itself_passes(self):
        self.assertEqual(0, self.run_main("0.90", "0.80"))

    def test_no_report_is_an_error(self):
        with tempfile.TemporaryDirectory() as d:
            self.assertEqual(2, cc.main([d]))


if __name__ == "__main__":
    unittest.main()
