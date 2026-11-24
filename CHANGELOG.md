# Changelog

What changed in each version of Scribe. Newest first. Every entry is written down; this file is no exception.

## 0.1.1 (2026-11-24)

- **Format version.** The Scribe node in the save now carries `schema = 1`. A save from 0.1.0 (no number) reads as version 1
  and loads in full; fields or nodes written by a newer Scribe are kept when an older one saves. A frozen 0.1.0 save is part of
  the tests.
- **Contract GUID and agency** on every contract entry (`contract`, `agent`), so two contracts with the same title can be told
  apart. **Role** (`role`: pilot, engineer, scientist) on kerbal entries.
- **Vessel and body only when they belong.** Funds, science and reputation changes used to carry the active vessel and body
  whenever you were in flight, even for a contract reward. Now only the reasons that belong to a vessel carry them (rollout,
  recovery, loss, science transmission, progress). Entries from 0.1.0 are not changed.
- A contract event without a title no longer raises an error outside the guarded block; a save node that is missing is ignored.
- Facility entries: the level is numbered as in the game's menus (the first level is level 1). This was already so; the README
  said "internal plus one" and now says it plainly.
- Code: the decisions that need no game (body name, Kerbal Construction Time detection, empty text, seeding a balance) moved
  to the core and are tested; one table of game events for hooking and unhooking; the plugin builds with warnings as errors.
- Tests: 120 unit tests (were 53), mutation score 87.06 % (was 72.39 %), a coverage floor in CI (90 % of lines, 80 % of
  branches; now 100 % and 94.9 %), property tests of the fold. In-game rounds now also cover a missing kerbal, failed and
  cancelled contracts, the contract GUID and the vessel rule: 9 rounds in 3 scenarios passed (about 0.9 microseconds per event).
- Repository: no path of the maintainer's machine in the workflows or scripts (the in-game CI job reads its folders from
  repository variables), `tools/check-coverage.py`, a README for `tools/`.

### Flown in a career save so far

Not yet with this version: it is installed in the author's career with the release and this section is updated after the first
sessions. The 0.1.0 text below still describes what was flown.

## 0.1.0 (2026-11-23)

- Ledger of funds, science and reputation changes (real change, new balance, KSP's reason), launches, recoveries,
  destroyed vessels, kerbal deaths, contracts and researched technologies, kept in the save.
- Adds itself to a save that was made without it (before, a test run on such a save never created the module).
- Vessels merged by docking are no longer counted as destroyed (a 3-second window after a coupling). Tested with an undock
  and re-dock of a station module.
- Facility upgrades are recorded (`facility`, `Upgraded`), once per upgrade.
- With Kerbal Construction Time installed, a tech it unlocks is recorded as `TechCompleted` (KCT queues the tech at
  research time and unlocks it later without the stock event). Found by name at run time; Scribe has no reference to KCT.
- Charges and refunds that KSP makes part by part in the same instant are one entry (summed change, last balance, `count`);
  credits and debits stay separate. In a real career save this is about a sixth of the entries.
- `tools/remove-from-save.py` removes the ledger from a save.
- Tests: unit tests and scripted in-game rounds (see `docs/TESTING.md`).

### Flown in a career save so far

Scribe runs in the author's own career (Kerbal Construction Time, ScrapYard and about a hundred other mods) and is updated there with every version, so this is a work in progress. The 13 Nov build recorded 801 entries over about ten Kerbin days, with 745 balance checks without a break and no errors in the log. The build in this version (grouping and the debris filter) passed the scripted rounds and is now installed in that career.

Scripted rounds in a test install (KSP 1.12.5, with Kerbal Construction Time and other mods loaded): the eight known
transactions with the right change and reason, a flight with a launch, a destruction, a recovery, a kerbal death and a
finished contract, a quickload that took the ledger back with the save, and a save without Scribe that got the module added.
An undock and re-dock of a station module (no false destruction), Kerbal Construction Time driven through its own code
(a vessel queued and finished, a tech queued and unlocked, a facility upgrade finished), and the mod removed from a save with
and without the ledger. Not covered: a long, finished career.
