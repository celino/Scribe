# Contributing to Scribe

Thanks for looking. Scribe is a small mod and the bar is simple: a change is ready when it is tested and the log is clean.

## Before you start
- Bug or idea? Open an issue first. Say what you saw, the KSP and
  Scribe versions, and attach `KSP.log` (check it for personal paths) and the `ScribeModule` part of `persistent.sfs`.
- Read the [decision records](docs/adr/README.md). If your change goes against one, say so in the issue.

## Set up
You need a KSP 1.12.5 you own only to build the plugin and to run the in-game rounds. The unit tests need only the .NET
SDK 8 and Python 3. See [`docs/TESTING.md`](docs/TESTING.md).

```sh
python3 tools/validate.py
python3 -I -m unittest discover -s tests -p 'test_*.py'
dotnet test tests/Scribe.Tests/Scribe.Tests.csproj -warnaserror --settings tests/coverlet.runsettings
```

## Rules
- **A feature has an ID.** New behavior: a row in `tests/FEATURES.md`, a line in the README, and a test named with the
  ID. `tools/validate.py` fails the build if one of the three is missing.
- **Logic goes in `Source/Core`**, with no KSP types. The plugin only adapts events. The plugin builds with C# 7.3.
- **Zero warnings.** The core builds under `-warnaserror` with the analyzers on. Follow `.editorconfig`.
- **Touching the plugin?** Run the in-game rounds (`tools/run-ingame-all.sh`) and say in the PR which passed. CI cannot.
- **Changelog.** User-visible change: a line under the unreleased section of `CHANGELOG.md` (Keep a Changelog).
- **Commits.** Short imperative subject, in the style of [Conventional Commits](https://www.conventionalcommits.org/)
  (`fix:`, `feat:`, `docs:`, `test:`). Versions follow [SemVer](https://semver.org/).
- **Language.** English in code, docs and messages.

## AI assistance
This project is made with AI assistance, and the maintainer reads, tests and answers for the result. If you use AI tools
for a contribution, that is fine; you still read it, run it and answer for it. Say so in the PR.

## Pull requests
Small and focused, with the evidence: what you ran and what it printed. Branch from `master`.
