# 0003. The logic lives in `Scribe.Core`, without KSP types

Status: Accepted (2026-11-13)

## Context
The first version put everything in one KSP plugin. Testing it meant starting the game, which takes minutes. Most of the
logic does not need the game: balances and deltas, folding old entries, which vessel destructions count as losses,
debouncing facility upgrades, reading and writing the node as text.

## Decision
Split the code in two projects:
- `Source/Core` (`Scribe.Core`): plain C#, no reference to KSP or Unity. Time is passed in as an argument.
- `Source/Scribe.csproj` (`Scribe.dll`): adapts KSP events and `ConfigNode` to the core.

The core targets `net48;netstandard2.0`. Plain `netstandard2.0` does not load in KSP 1.12 (no `netstandard.dll` in its
Mono), so the `net48` build is the one that ships. The plugin stays on C# 7.3 because that is what KSP 1.12 compiles.

## Consequences
- The xUnit tests run in seconds on any machine with the .NET SDK, and the core is almost fully covered.
- The release has two DLLs: `Scribe.dll` and `Scribe.Core.dll`.
- The adapter is thin, so what the unit tests cannot see is covered by the in-game rounds ([0005](0005-in-game-tests-run-locally-only.md)).
