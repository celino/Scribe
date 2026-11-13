# Changelog

What changed in each version of Scribe. Newest first. Every entry is written down; this file is no exception.

## 0.1.0 (unreleased)

- Ledger of funds, science and reputation changes (real change, new balance, KSP's reason), launches, recoveries,
  destroyed vessels, kerbal deaths, contracts and researched technologies, kept in the save.
- Adds itself to a save that was made without it (before, a test run on such a save never created the module).
- Vessels merged by docking are no longer counted as destroyed (a 3-second window after a coupling). Tested with an undock
  and re-dock of a station module.
- Facility upgrades are recorded (`facility`, `Upgraded`), once per upgrade.
- With Kerbal Construction Time installed, a tech it unlocks is recorded as `TechCompleted` (KCT queues the tech at
  research time and unlocks it later without the stock event). Found by name at run time; Scribe has no reference to KCT.
- `tools/remove-from-save.py` removes the ledger from a save.

### Flown in a career save so far

Scripted rounds in a test install (KSP 1.12.5, with Kerbal Construction Time and other mods loaded): the eight known
transactions with the right change and reason, a flight with a launch, a destruction, a recovery, a kerbal death and a
finished contract, a quickload that took the ledger back with the save, and a save without Scribe that got the module added.
An undock and re-dock of a station module (no false destruction), Kerbal Construction Time driven through its own code
(a vessel queued and finished, a tech queued and unlocked, a facility upgrade finished), and the mod removed from a save with
and without the ledger. Not played: a long career.
