# Security

Scribe is a KSP plugin with no network code, no external dependencies and no files outside your KSP folder. It reads
game events and writes one node (`ScribeModule`) into the save. The realistic problems are bugs that corrupt a save.

## Reporting
If you find a way to make Scribe damage a save, read or write outside the game folder, or anything else you think is
sensitive, **do not post it in a public issue**. Report it privately with *Report a vulnerability* on the repository's Security tab, with the steps and your versions. Expect an answer within a week. This is a hobby project, so
there is no bounty and no formal SLA.

For ordinary bugs, use an issue. Attach your `KSP.log` after checking it for personal paths.

## Supported versions
Only the latest release. Scribe supports KSP 1.12.x.

## Supply chain
Zero runtime dependencies. The build uses the .NET SDK, xUnit and coverlet for tests only; none of them is in the release
zip. Each release zip has a `.sha256` file next to it.
