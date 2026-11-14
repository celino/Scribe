# 0004. Cap the ledger at 5,000 entries and fold the oldest

Status: Accepted (2026-11-13)

## Context
The ledger is stored in the save ([0001](0001-ledger-lives-in-the-save.md)). A long career has tens of thousands of
fund events. An unbounded node would slow loading and saving.

## Decision
Keep at most 5,000 entries (about 125 bytes each, roughly 630 KB). When the cap is passed, fold the oldest entries into
one summary per Kerbin day and type, about 10% extra so it does not run on every add. A summary keeps the summed delta
and the last balance. If a day-per-type summary still does not fit, the bucket widens until it does.

## Consequences
- Recent history is exact; old history is coarser but still adds up.
- A property test checks that folding preserves the sum of deltas and the count for any sequence.
- Measured with 6,000 fund events: about 1.7 µs per event; a save took the same time with and without Scribe.
