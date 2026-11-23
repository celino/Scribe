# Architecture decision records

Short notes on why Scribe is built the way it is. One decision per file, written when it was made. A decision that
changes gets a new record that supersedes the old one; old records are not rewritten.

| # | Decision | Status |
|---|---|---|
| [0001](0001-ledger-lives-in-the-save.md) | The ledger lives in the save, as a scenario node | Accepted |
| [0002](0002-no-window-no-settings.md) | No window and no settings | Accepted |
| [0003](0003-core-separate-from-the-plugin.md) | The logic lives in `Scribe.Core`, without KSP types | Accepted |
| [0004](0004-cap-and-fold-the-ledger.md) | Cap the ledger at 5,000 entries and fold the oldest | Accepted |
| [0005](0005-in-game-tests-run-locally-only.md) | In-game tests run locally, never on the public CI | Accepted |
| [0006](0006-group-part-by-part-changes.md) | Group the changes KSP reports part by part | Accepted |

Format: Context, Decision, Consequences. Keep each under one page.
