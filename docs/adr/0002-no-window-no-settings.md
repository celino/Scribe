# 0002. No window and no settings

Status: Accepted (2026-11-13)

## Context
Readable diaries already exist (Kaptain's Log, Space Age's Chronicle). What was missing is structured numbers: when,
how much, to what balance, and the reason KSP reports.

## Decision
Scribe has no user interface, no settings file and no player-facing text. It reacts to game events and writes data.

## Consequences
- Nothing runs every frame, and there is nothing to configure or translate.
- The README says plainly that the ledger is not visible in game. A window is not planned; other mods can build one.
- The surface to test is small: events in, entries out.
