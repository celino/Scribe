# tools

Scripts for building, testing and packaging Scribe, and for removing its ledger from a save. None of them is needed to
*use* the mod; players only need the release zip.

Run everything from the repository root (`sh tools/package.sh`, `python3 tools/validate.py`).

## Checks and packaging

| File | What it does | Needs |
|---|---|---|
| `validate.py` | Fails early: the feature matrix (`tests/FEATURES.md`), the README and the unit tests agree; `Scribe.version` is complete valid JSON; the docs have no broken links or leftover placeholders. `--release` (pull requests) and `--tag vX.Y.Z` also fail while the README still has the release-link placeholder, and check the tag against the version and the CHANGELOG. | Python 3 |
| `package.sh` | Builds `dist/Scribe-<version>.zip` (with `GameData/` at the root, the layout SpaceDock and CKAN expect, plus README, CHANGELOG and LICENSE) and its `.sha256`. The DLLs are not in git: build `Source/` first and copy `Source/out/*.dll` to `GameData/Scribe/Plugins/`. | Python 3 |
| `check-coverage.py` | Fails when the unit-test coverage of the core is below the floor (90 % of lines, 80 % of branches). Reads the report `dotnet test` writes with `tests/coverlet.runsettings`. | Python 3 |
| `release-notes.py` | Prints the CHANGELOG section of one version, for the release notes. | Python 3 |

## Working with a save

| File | What it does | Needs |
|---|---|---|
| `remove-from-save.py` | Removes the Scribe block from a save. Without `--apply` it is a dry run that only says what it would remove; with `--apply` it first writes `<file>.bak-scribe`. Close KSP first. KSP keeps the data of a removed mod in the save, so use this if you want the ledger gone from `persistent.sfs`. See "Can I remove it?" in the README. | Python 3 |

## In-game test rounds (layer 3 of the tests)

These start a real KSP 1.12.5 in a window, so they run on a machine with a display and a GPU, never on the public CI.
How they fit together is in [`docs/TESTING.md`](../docs/TESTING.md) and [ADR 0005](../docs/adr/0005-in-game-tests-run-locally-only.md).

| File | What it does | Needs |
|---|---|---|
| `ksp-dev-create.sh` | Makes a throw-away test install from a KSP 1.12.5 copy you own: the game is hard-linked (about 3 MB extra), `GameData` holds only Squad plus the mods of one scenario (`minimal`, `contracts`, `kct`, from `tests/ingame/scenarios.json`, installed through CKAN), and the synthetic save is added. The original game is never modified. | A KSP 1.12.5 copy, CKAN, Python 3 |
| `run-ingame.sh` | Runs one scripted round (`tx`, `revert`, `flight`, `dock`, `bench`, `menu`, `noscribe`, `kct`) in such an install and prints `PASS` or `FAIL` from the driver's `ASSERT Fnn` lines. | `DISPLAY`, the .NET SDK or the Docker image from `tests/ingame/docker` |
| `run-ingame-all.sh` | Runs every round of the chosen scenarios, one install per scenario, and prints one line per round. It works in `ksp-test/run-all` by default and **deletes the installs it creates** there when it finishes (`--keep` keeps them; `--work` picks another folder). | same as above |
| `ksp-ingame-ci.sh` | The maintainer's own test-server script: the same rounds inside the Docker image, on an overlay of a prepared install. Not meant for other machines. | Docker, a prepared install |

## Not here

The ledger is plain text in `persistent.sfs`, so any script can read it. This folder does not ship a reader yet.
