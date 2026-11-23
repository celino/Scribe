# 0006. Group the changes KSP reports part by part

Status: Accepted (2026-11-23)

## Context
KSP charges a rollout and refunds a recovery one part at a time, in the same instant, each with the same reason. In a real
career save (801 entries, about ten Kerbin days) 93% of the entries sat in 36 groups of the same type, reason and
second: one group had 80 entries, another 58 that summed to zero. Such lines say nothing more than one line would, and
they fill the 5,000-entry cap ([0004](0004-cap-and-fold-the-ledger.md)) quickly.

## Decision
A balance change joins an earlier entry when type, reason, vessel, body and sign are the same and it happened within one
game second (looking back at most 8 entries). The entry keeps the summed change, the last balance and a `count`. Credits
and debits of the same reason are **not** netted: a recovery with one credit and thirty debits must stay readable.
Events (launch, kerbal, contract, tech) are never grouped.

## Consequences
- The same career is about a sixth of the size, and the cap lasts several times longer.
- Which part was charged is not recorded; it never was (KSP gives no part).
- Old ledgers load unchanged (`count` was already an optional field).
