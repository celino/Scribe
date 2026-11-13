<p align="center"><img alt="A small green Kerbal in a feathered cap sits on a supply crate at sunrise, writing in a giant ledger beside a rocket on its launch pad" src="docs/images/scribe-ledger.jpg"></p>

<h1 align="center">Scribe</h1>

<p align="center"><i>The funds went down by 300. Scribe wrote down why.</i></p>

<p align="center">
  <img alt="KSP 1.12" src="https://img.shields.io/badge/KSP-1.12-1e6fb8">
  <img alt="No dependencies" src="https://img.shields.io/badge/dependencies-none-6a3fb5">
  <img alt="License: MIT" src="https://img.shields.io/badge/license-MIT-555">
  <img alt="Made with AI assistance, reviewed and tested by a human" src="https://img.shields.io/badge/made%20with-AI%20assistance-8a8a8a">
  <img alt="Status: test build, not played in a long career" src="https://img.shields.io/badge/status-test%20build-c0392b">
</p>

> **Status: test build 0.1.0.** Scribe only reads game events and writes one node in your save, so it is low risk, but
> back up your save before trying it. So far it has run in a test install of KSP 1.12.5 with scripted transactions, a
> flight (launch, destruction, recovery, a kerbal death, a finished contract), a quickload, an undock and re-dock of a
> space station, Kerbal Construction Time queues (vessel, tech, facility upgrade) and the removal of the mod from a save.
> It has **not** been played through a long career. Do not rely on it yet.

Scribe keeps a ledger inside your save: **when** funds, science or reputation changed, by **how much**, to **what
balance**, and **why** (the reason KSP itself reports, like `ContractReward` or `RnDPartPurchase`). It also notes
launches, recoveries, destroyed vessels, kerbal deaths and hirings, contracts and researched technologies. It has **no
window** and no settings: it is data for other tools, and plain text you can read in `persistent.sfs`.

## What it does

- **Real change, not just the balance.** KSP's `OnFundsChanged`, `OnScienceChanged` and `OnReputationChanged` hand mods
  the *new balance*. Scribe keeps the previous one and records the difference.
- **Lives in the save.** The ledger is in `persistent.sfs` (scenario `ScribeModule`), so **revert, quickload and save
  backups take it along**. An entry made in a flight you reverted is gone, as it should be.
- **Light.** It only reacts to game events; nothing runs every frame. In a test with 6,000 fund events it added about
  1.7 µs per event, and saving the game took the same time (353 ms without, 355 ms with).
- **Bounded.** It keeps up to 5,000 entries (about 125 bytes each, roughly 630 KB at the limit). Older entries are folded
  into one summary per Kerbin day.
- **Adds itself.** Put it in a save that never had it: Scribe adds its scenario when you reach the Space Center.

| It records | With |
|---|---|
| Funds, science, reputation | change, new balance, KSP's reason, vessel and body if in flight |
| Launch, recovery, destruction | vessel name and body (debris and flags ignored) |
| Kerbal dead or missing, kerbal hired | kerbal name |
| Contract accepted, completed, failed, cancelled | contract title |
| Technology researched | tech id |
| Technology unlocked by Kerbal Construction Time (when it is installed) | tech id |
| Facility upgraded | facility id and level |

## Requirements

- KSP 1.12.x (tested on 1.12.5)
- No other mods. No known conflicts: it was run in a test install with Contract Configurator, Kerbal Construction Time
  and about a hundred other mods loaded. Kerbal Construction Time is optional (see the FAQ).

## Install

Copy the `GameData/Scribe` folder from the zip into your KSP `GameData`. That's it. There is no public download yet: test builds are
zips I make myself (see "Building from source").

## FAQ

**Is it a replacement for Kaptain's Log?**
No. [Kaptain's Log](https://github.com/linuxgurugamer/KaptainsLog) is a readable diary with a window, notes and
screenshots, kept in a file outside the save. Scribe is only structured numbers inside the save. They complement each other.

**And Space Age?**
[Space Age](https://github.com/GarwelGarwel/SpaceAge) keeps a Chronicle of events (launches, recoveries, deaths,
technologies) in the save, but, from reading its code (v1.3.8), not the funds, science and reputation changes or their
reasons. Scribe fills that gap.

**Can I see the ledger in the game?**
Not yet. It is plain text in `persistent.sfs`; scripts can read it. A window is not planned.

**Can I remove it?**
Yes, nothing else depends on it. I tested it: with the `GameData/Scribe` folder gone and the ledger still in the save, the
game loads normally and logs one harmless line (`Cannot find a Module of typename 'ScribeModule'`), and it writes the
ledger back unchanged on every save. To clean the save as well, delete the whole `SCENARIO` block with `name = ScribeModule`
from `persistent.sfs` (and from any quicksave you want to keep), or run `tools/remove-from-save.py --apply <save folder>`
from the source repo with KSP closed (it keeps a `.bak-scribe` copy and, without `--apply`, only tells you what it would
remove). Tested: with the block removed the game loads with no error and no trace of Scribe. Put the mod back later with
the ledger still in the save and it picks up where it stopped.

**Does it work with docking?**
Yes. When the game merges two docked vessels it destroys one of them, and Scribe ignores that. Tested by undocking a
space station module and letting the game dock it again: the game fired the destroy event for the merged 24-part vessel
and the ledger stayed empty. A vessel that is really lost is still recorded.

**And Kerbal Construction Time?**
Optional. KCT queues a researched tech and unlocks it later without telling the game, so with KCT Scribe writes two
entries: `TechResearched` when you pay and queue it, and `TechCompleted` when KCT unlocks it. Funds KCT charges (building a
vessel, rollout, facility upgrades) are recorded like any other with KSP's reason, but **KCT does not announce when a
vessel finishes building or enters its queue, so Scribe cannot record those moments**; you only see the funds line.

## Known issues

- Not tested: sandbox or science-only saves, a large long-running career, and the 5,000-entry limit in a real game (only
  in a synthetic test).
- `Missing` kerbals, hired kerbals and contracts other than "completed" were not exercised in a flight yet.
- Vessels finished in the Kerbal Construction Time queue are not recorded (KCT has no event for it); see the FAQ.
- The facility entry shows the new level as the game counts it internally, plus one.

## Reporting problems

Bugs and questions: GitHub Issues, once the repo is public. Until then, tell Evandro. Attach your `KSP.log` (check it for
personal paths first) and, if you can, the `ScribeModule` part of `persistent.sfs`.

## Localization

Scribe has no player-facing text (no window, no messages), so there is nothing to translate. The ledger uses KSP's own
reason names and your vessel and kerbal names.

## Testing

`tests/FEATURES.md` lists every feature this README promises, the layer that proves it and the test. `tools/validate.py`
checks the `.version`, the CHANGELOG, and that the matrix matches the README and the tests. `sh tests/run.sh` runs 22 xUnit
tests on the ledger logic (balance differences, trimming, saving and loading; about 99% of the core's lines), with only the
dotnet SDK and no KSP. `tools/remove-from-save.py`
takes the ledger out of a save. The parts that need the game
run only on my own test machine, in a copy of KSP with a scripted driver; the results are in the CHANGELOG under
"Flown in a career save so far", and the honest summary is: scripted rounds yes, a long career no.

## Building from source

`dotnet build -c Release` in `Source/` with `KSP_DIR` pointing to a KSP install (it needs `Assembly-CSharp.dll` and the
UnityEngine assemblies). The DLLs (`Scribe.dll` and `Scribe.Core.dll`) are not in git; the release zip is the way to get them. `sh tools/package.sh` builds
`dist/Scribe-<version>.zip` from a local build.

## How this was made

I'm Evandro, and I make this with Claudinho, my AI assistant (Claude, by Anthropic). We plan together and nothing gets
built until I approve it; Claudinho writes most of the code and the first drafts of the texts and runs the automated
checks; I review every change and play it in a test save. The opening image was made with AI (GPT Image 2.5), from a prompt written by my media assistant, and reviewed by me. Anything that still blows up on the pad is on me.

## Credits

- **linuxgurugamer**, who made **Kaptain's Log**: reading it showed me what a game-event diary has to catch.
- **GarwelGarwel**, who made **Space Age**, whose Chronicle I read to see what was already covered.
- **Claudinho**, my AI assistant (Claude, by Anthropic), who wrote most of the code and the first drafts of the texts.
  Every line was reviewed and tested by Evandro, who is the one responsible for all of it, mistakes included.

## License

[MIT](LICENSE).

---

<sub>Made by Evandro, a.k.a. *Fogueteiro da Holanda* 🇧🇷 🇳🇱 ·
[GitHub](https://github.com/celino) · [Buy me a coffee](https://buymeacoffee.com/fogueteiro_da_holanda)</sub>
