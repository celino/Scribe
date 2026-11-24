<p align="center"><img alt="A small green Kerbal in a feathered cap sits on a supply crate at sunrise, writing in a giant ledger beside a rocket on its launch pad" src="docs/images/scribe-ledger.jpg"></p>

<h1 align="center">Scribe</h1>

<p align="center"><i>The funds went down by 300. Scribe wrote down what changed, and the reason KSP gave.</i></p>

<p align="center">
  <img alt="KSP 1.12" src="https://img.shields.io/badge/KSP-1.12-1e6fb8">
  <img alt="No dependencies" src="https://img.shields.io/badge/dependencies-none-6a3fb5">
  <img alt="License: MIT" src="https://img.shields.io/badge/license-MIT-555">
  <img alt="Made with AI assistance, reviewed and tested by a human" src="https://img.shields.io/badge/made%20with-AI%20assistance-8a8a8a">
  <img alt="Status: work in progress, played in my own career" src="https://img.shields.io/badge/status-test%20build-c0392b">
</p>

> **Status: work in progress, 0.1.1.** Scribe only reads game events and writes one node in your save, so it is low
> risk, but back up your save before trying it. I play it in my own career save (Kerbal Construction Time, ScrapYard
> and about a hundred other mods) and update my install with every version, so expect changes for a good while. It has
> also run in scripted rounds in a test install (see Testing). Do not rely on it for a record you cannot lose.

Scribe keeps a ledger inside your save: **when** funds, science or reputation changed, by **how much**, to **what
balance**, and **why** (the reason KSP itself reports, like `ContractReward` or `RnDPartPurchase`). It also notes
launches, recoveries, destroyed vessels, kerbal deaths and hirings, contracts and researched technologies. It has **no
window** and no settings: it is data for other tools, and plain text you can read in `persistent.sfs`.

## Requirements

- KSP 1.12.x (tested on 1.12.5)
- No other mods. No known conflicts (run with Contract Configurator, Kerbal Construction Time and about a hundred other
  mods). Not tested with RP-1 or Kerbalism. Kerbal Construction Time is optional (see the FAQ).

## Install

Download `Scribe-0.1.1.zip` from the [latest release](https://github.com/celino/Scribe/releases/latest) and copy its `GameData/Scribe` folder into your KSP
`GameData`. It holds two DLLs, `Scribe.dll` and `Scribe.Core.dll`; keep both. That's it.

## What it does

- **Real change, not just the balance.** KSP's `OnFundsChanged`, `OnScienceChanged` and `OnReputationChanged` hand mods
  the *new balance*. Scribe keeps the previous one and records the difference.
- **Lives in the save.** The ledger is in `persistent.sfs` (scenario `ScribeModule`), so **revert, quickload and save
  backups take it along**. An entry made in a flight you reverted is gone, as it should be.
- **Light.** It only reacts to game events; nothing runs every frame.
- **Bounded.** It keeps up to 5,000 entries (about 160 bytes each, roughly 800 KB at the limit). Older entries are folded
  into one summary per Kerbin day.
- **One line per instant.** KSP charges and refunds a vessel part by part, in the same instant. Scribe writes one line per
  instant, reason and sign, with the summed change and a `count` (credits and debits stay separate).
- **Format version.** The save node carries a `schema` number (1 today). A save from 0.1.0 has none and reads as version 1;
  fields written by a newer Scribe are kept, not dropped, if you go back to an older one.
- **Adds itself.** Put it in a save that never had it: Scribe adds its scenario when you reach the Space Center.

| It records | With |
|---|---|
| Funds, science, reputation | change, new balance, KSP's reason; vessel and body only when the reason belongs to a vessel (rollout, recovery, loss, science transmission, progress), so a contract reward in flight is not charged to the ship |
| Launch, recovery, destruction | vessel name and body (debris and flags ignored) |
| Kerbal dead or missing, kerbal hired | kerbal name and role (pilot, engineer, scientist) |
| Contract accepted, completed, failed, cancelled | contract title, contract GUID and the agency that offered it |
| Technology researched | tech id |
| Technology unlocked by Kerbal Construction Time (when it is installed) | tech id |
| Facility upgraded | facility id and level, numbered as in the game's menus (the first level is level 1) |

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
Yes, nothing else depends on it. Delete `GameData/Scribe`; the game logs one harmless line and loads normally. To clean the
save too, see [`docs/TESTING.md`](docs/TESTING.md) (`tools/remove-from-save.py`). Put the mod back later and it picks up
where it stopped.

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

- Not tested: sandbox or science-only saves and a large long-running career. (The 5,000-entry limit was exercised in a
  real game with 12,000 scripted events, in a test save.)
- `Missing` kerbals and contracts other than "completed" (failed, cancelled) are covered by scripted in-game rounds, but not yet seen in a real career save. Hired kerbals and completed contracts were seen in a real career save.

## Reporting problems

Questions and ideas: the [forum thread](https://forum.kerbalspaceprogram.com/topic/232203-112x-scribe-a-ledger-in-your-save-of-every-funds-science-and-reputation-change-with-the-reason-ksp-gave-work-in-progress-010/). Bugs: open an issue (the bug-report form asks for your versions and `KSP.log`). Check the log for personal paths before you
attach it and, if you can, add the `ScribeModule` part of `persistent.sfs`.

## Contributing

See [`CONTRIBUTING.md`](CONTRIBUTING.md), the [decision records](docs/adr/README.md) (why it is built this way), the
[code of conduct](CODE_OF_CONDUCT.md) and [`SECURITY.md`](SECURITY.md).

## Localization

Scribe has no player-facing text (no window, no messages), so there is nothing to translate. The ledger uses KSP's own
reason names and your vessel and kerbal names.

## Testing

Every feature above has an ID in [`tests/FEATURES.md`](tests/FEATURES.md) and a test. `sh tests/run.sh` runs the unit
tests with only the .NET SDK; scripted in-game rounds run only on my own test machine. Honest summary: scripted rounds
yes, and my own career, which is still going; it is not a long, finished one. Details: [`docs/TESTING.md`](docs/TESTING.md).

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
