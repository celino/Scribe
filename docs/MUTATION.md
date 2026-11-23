# Mutation testing

Line and branch coverage say which code the tests *run*. Mutation testing asks a harder question: if the code
were subtly wrong, would a test notice? [Stryker.NET](https://stryker-mutator.io/) edits the core (flips a `<` to `<=`,
empties a string, removes a statement) and re-runs the unit tests. A mutant that no test fails on has *survived*.

This is a report, not a gate. The score is not a release criterion; what matters is why each group survived.

## How to run it

```sh
cd tests/Scribe.Tests
dotnet tool install -g dotnet-stryker --version 4.8.1   # 4.8.1 is the one used below; the latest needs a newer SDK
dotnet stryker                                          # reads stryker-config.json; reports in StrykerOutput/
```

Only the core (`Source/Core`, plain C#, no KSP types) is mutated. The KSP module cannot be unit tested without the
game; the in-game rounds cover it (see [TESTING.md](TESTING.md)).

## Result (23 November 2026, Stryker.NET 4.8.1, 53 unit tests)

| | |
|---|---|
| Mutants created | 344 |
| Not tested (compile errors, already-covered blocks, no coverage) | 84 (36 + 40 + 8) |
| Tested | 260 |
| Killed (incl. 11 timeouts) | 194 |
| Survived | 66 (64 in `ScribeCore.cs`, 2 in `ScribeRules.cs`) |
| **Mutation score** | **72.39 %** |

Unit-test coverage at the same commit: 100 % of lines (251/251), 95.5 % of branches (191/200).

## What survived, and why

| Group | Where | What it means | Verdict |
|---|---|---|---|
| Default values of `Entry` fields (`Type = ""`, `Reason = ""`, `Vessel = ""`, `Body = ""`) | `ScribeCore.cs` 21-26 | Every code path sets these fields explicitly, so the default is never read. | Equivalent mutants: no test can tell. Harmless. |
| Boundary operators (`<` vs `<=`) in the group window and lookback | 93, 96, 99 | No test puts two events exactly `GroupWindow` seconds apart, or exactly `GroupLookback` entries back. | **Real gap**: add boundary tests. |
| Boundary operators in the cap check | 115, 124, 125 | The tests exceed the cap by a margin; none sits exactly on it. `Count == Cap` must not prune. | **Real gap**: add a test at `Cap` and at `Cap + 1`. |
| Fold arithmetic (`Cap / 10`, `Math.Max`, `Math.Min`) | 126, 131 | The tests prove the cap and the sums survive a prune, not how many entries a prune folds. | **Real gap** only if the "fold at least 10 %" figure is a promise; today it is an implementation detail. |
| Fold levels and bucket keys | 138-160 | The tests check totals and the final count after folding, not the shape of the result (which day a summary lands on, whether the type is kept at level 20 or merged at 41, the `folded N` text, the `bucket` doubling). | **Real gap**: the shape of the folded ledger is what a reader of `ledger.txt` sees. A shape test would pin it. |
| Serialization of optional fields (`type`, `vessel`, `body`, `text`) | 175-181 | A round trip does not fail when a field is dropped or when an empty field is written. | **Real gap**, and the cheapest to close: compare every field after a round trip, and assert that empty fields are omitted. |
| `IsBalance` | 32 | The test data does not tell `funds`/`science`/`reputation` apart from other types where it matters. | **Real gap**, small. |
| `Unescape` edge cases and `Escape` of `\r` | 249-260 | A trailing backslash and a lone `\r` are never round-tripped. | **Real gap**: affects a vessel name with unusual characters. |
| Rules: initial `lastAt = -10f` and `<` vs `<=` on the repeat window | `ScribeRules.cs` 52, 56 | The first message is never tested right at the window edge. | Boundary; harmless in play, cheap to test. |
| `NoCoverage` (8) | 71, 72, 107, 125 | Strings of constructors and one early `return` the tests never reach. | Same family as the defaults above, plus one reachable `return` at 125. |

Nothing here is a bug in the mod: the tests pass and the in-game rounds agree. The survivors say where the tests are
weaker than the 100 % line coverage suggests: **boundaries and the shape of the folded ledger**.

## Not done

No tests were added for the survivors (this document only reports them), and the module (`Source/Plugin`) is not mutated.
