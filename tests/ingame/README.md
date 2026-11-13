# In-game test inputs

- `make-save.py` builds `save/`, a synthetic career (Normal difficulty, 100,000 funds, 50 reputation, 100 science,
  four stock kerbals, no vessels, no contracts) plus `Ships/VAB/Scribe Probe.craft`, a stock probe. Nothing comes from a
  real career. Regenerate with `python3 -I tests/ingame/make-save.py --craft <any stock probe .craft>`.
- `scenarios.json` lists the mods each scenario installs (CKAN id + version) and the rounds it runs.
- The Scribe scenario is deliberately not in the save: the module adds itself (scenario `menu`), or the runner seeds it.

Needs a licensed KSP 1.12.5; never runs on GitHub Actions. See `docs/TESTING.md` (planned) for the three test levels.
