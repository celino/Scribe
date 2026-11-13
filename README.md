<!-- Opening image (2:1, docs/images/). Not made yet: asked the Produtora. Remove this comment when it exists. -->

<h1 align="center">Scribe</h1>

<p align="center"><i>The funds went down by 300. Scribe wrote down why.</i></p>

<p align="center">
  <img alt="KSP 1.12" src="https://img.shields.io/badge/KSP-1.12-1e6fb8">
  <img alt="No dependencies" src="https://img.shields.io/badge/dependencies-none-6a3fb5">
  <img alt="License: MIT" src="https://img.shields.io/badge/license-MIT-555">
  <img alt="Made with AI assistance, reviewed and tested by a human" src="https://img.shields.io/badge/made%20with-AI%20assistance-8a8a8a">
  <img alt="Status: test build, not played in a long career" src="https://img.shields.io/badge/status-test%20build-c0392b">
</p>

> **Status: test build 0.1.0.** Scribe has run in a test install of KSP 1.12.5 with scripted transactions, a flight
> (launch, destruction, recovery, a kerbal death, a finished contract) and a quickload. It has **not** been played
> through a long career, **not** tested with docking, and **not** tested with funds spent through Kerbal Construction
> Time queues. It is safe in the sense that it only reads game events and writes one node in the save, but a save backup
> before you try it is cheap. Do not rely on it yet.

Scribe keeps a ledger inside your save: **when** funds, science or reputation changed, by **how much**, to **what
balance**, and **why** (the reason KSP itself reports, like `ContractReward` or `RnDPartPurchase`). It also notes
launches, recoveries, destroyed vessels, kerbal deaths and hirings, contracts and researched technologies. It has no
window and no settings; the ledger is data for other tools and for you to read in `persistent.sfs`.

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

## Requirements

- KSP 1.12.x (tested on 1.12.5)
- No other mods. It works next to Contract Configurator, Kerbal Construction Time and others; the ones I run are the ones
  it was tested with.

## Install

Copy the `GameData/Scribe` folder from the zip into your KSP `GameData`. That's it. There is no public download yet: test builds are
zips I make myself (see "Building from source").

## FAQ

**Is it a replacement for Kaptain's Log?**
No. [Kaptain's Log](https://github.com/linuxgurugamer/KaptainsLog) is a readable diary with a window, notes and
screenshots, kept in a file outside the save. Scribe is only structured numbers inside the save. They complement each other.

**Can I see the ledger in the game?**
Not yet. It is plain text in `persistent.sfs`; scripts can read it. A window is not planned.

**Can I remove it?**
The ledger node stays in your save as data the game ignores. Removing the mod from an existing save was **not tested**.

**Does it work with docking?**
Merging two vessels should not count as a destruction (there is a short guard for that), but I have not tested it in flight.

## Known issues

- Not tested: docking, Kerbal Construction Time queues, sandbox or science-only saves, a large long-running career, and
  the 5,000-entry limit in a real game (only in a synthetic test).
- `Missing` kerbals, researched technologies, hired kerbals and contracts other than "completed" were not exercised
  in a flight yet.
- Kaptain's Log, which I read for comparison, writes the new balance where it says "increased by"; that is why Scribe
  exists, not a request for anything from it.

## Localization

Scribe has no player-facing text (no window, no messages), so there is nothing to translate. The ledger uses KSP's own
reason names and your vessel and kerbal names.

## Testing

`tools/validate.py` checks the `.version` and that the CHANGELOG has the section for that version. `tests/run.sh` runs 24
checks on the ledger logic (balance differences, trimming, saving and loading), without KSP. The parts that need the game
run only on my own test machine, in a copy of KSP with a scripted driver; the results are in the CHANGELOG under
"Flown in a career save so far", and the honest summary is: scripted rounds yes, a long career no.

## Building from source

`dotnet build -c Release` in `Source/` with `KSP_DIR` pointing to a KSP install (it needs `Assembly-CSharp.dll` and the
UnityEngine assemblies). The DLL is not in git; the release zip is the way to get it. `sh tools/package.sh` builds
`dist/Scribe-<version>.zip` from a local build.

## How this was made

I'm Evandro, and I make this with Claudinho, my AI assistant (Claude, by Anthropic). We plan together and nothing gets
built until I approve it; Claudinho writes most of the code and the first drafts of the texts and runs the automated
checks; I review every change and play it in a test save. Anything that still blows up on the pad is on me.

## Credits

- **linuxgurugamer**, who made **Kaptain's Log**: reading it showed me what a game-event diary has to catch, and where the
  balance-versus-change trap is.
- **Claudinho**, my AI assistant (Claude, by Anthropic), who wrote most of the code and the first drafts of the texts.
  Every line was reviewed and tested by Evandro, who is the one responsible for all of it, mistakes included.

## License

[MIT](LICENSE).

---

<sub>Made by Evandro, a.k.a. *Fogueteiro da Holanda* 🇧🇷 🇳🇱 ·
[GitHub](https://github.com/celino) · [Buy me a coffee](https://buymeacoffee.com/fogueteiro_da_holanda)</sub>
