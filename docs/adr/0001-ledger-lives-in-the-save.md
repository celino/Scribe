# 0001. The ledger lives in the save, as a scenario node

Status: Accepted (2026-11-13)

## Context
Scribe records what changed funds, science and reputation, and why. KSP lets players revert a flight, quickload and
restore a save backup. A ledger kept in a separate file would not follow any of those: reverting a flight would leave
entries for money that was never spent.

## Decision
Keep the ledger in `persistent.sfs`, in a `ScribeModule` scenario node, written and read through the normal
`ScenarioModule` save and load. If the save has no node, Scribe adds one when the player reaches the Space Center.

## Consequences
- Revert, quickload and backups take the ledger along. Test F07 proves it.
- The ledger is plain text the player can read, and can remove (`tools/remove-from-save.py`, or delete the block).
- The save grows with the ledger, hence [0004](0004-cap-and-fold-the-ledger.md).
- Other mods and tools can read it without knowing about Scribe's code.
