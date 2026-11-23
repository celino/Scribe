# Feature matrix

Every feature the README promises has an ID, the layer that proves it and the test that does. `tools/validate.py` fails
when a row marked `automated` cites a test that does not exist, or when its README phrase is gone from the README.

Status: `automated` = unit test, `ingame` = a scripted round prints `ASSERT Fnn PASS` (tests/ingame), `planned` = not yet (no row uses it today).

Layers (what is tested; not the same as the "Step 1/2/3" of `docs/TESTING.md`, which is what you need to run it): **L2** = unit tests of the ledger core (`dotnet test`, no KSP), **L3** = decision logic of the module, tested
without KSP, **L4** = scripted rounds in a real game (local machine only, never on GitHub).

| ID | Feature | README phrase | Layer | Status | Test |
|---|---|---|---|---|---|
| F01 | The change is the balance difference, for funds, science and reputation | Scribe keeps the previous one and records the difference | L2 | automated | `F01_` |
| F02 | First event without a known balance is only remembered; an unchanged balance writes nothing | records the difference | L2 | automated | `F02_` |
| F03 | Cap of 5,000 entries; older ones fold into one summary per Kerbin day, keeping the total and the count | Older entries are folded | L2 | automated | `F03_` |
| F04 | The cap holds even when the days are far apart | Older entries are folded | L2 | automated | `F04_` |
| F05 | The ledger survives save and load unchanged (numbers, names, quotes, newlines, `//`) | plain text you can read in `persistent.sfs` | L2 | automated | `F05_` |
| F06 | Summaries survive save and load | Older entries are folded | L2 | automated | `F06_` |
| F07 | Revert and quickload take the ledger along | revert, quickload and save backups take it along | L4 | ingame | scenario `revert` |
| F08 | Launch, recovery and destruction recorded with the real vessel name | Launch, recovery, destruction | L4 | ingame | scenario `flight` |
| F09 | Docking is not recorded as a loss (rule tested in L3; the round couples two real vessels with Part.Couple) | Does it work with docking? | L3 + L4 | ingame | scenario `dock` |
| F10 | Kerbal death and hiring, contracts, researched technology | Kerbal dead or missing | L4 | ingame | scenario `flight` |
| F11 | Kerbal Construction Time: tech completed and facility upgrade | Technology unlocked by Kerbal Construction Time | L4 | ingame | scenario `kct` |
| F12 | The scenario is added to a save that never had it | Adds itself | L4 | ingame | scenario `menu` |
| F13 | About 2 µs per event; saving is not slower | Light | L4 | ingame | scenario `bench` |
| F14 | The game loads with the mod removed and the ledger still in the save | Can I remove it? | L4 | ingame | scenario `noscribe` |
| F15 | `tools/remove-from-save.py` takes the ledger out of a save and keeps a backup | remove-from-save.py | L2 | automated | `test_F15_` |
| F16 | Destruction: debris, flags and merged vessels ignored; one entry per vessel | debris and flags ignored | L3 | automated | `F16_` |
| F17 | Facility upgrade: the same upgrade fired twice is one entry | facility id and level | L3 | automated | `F17_` |
| F18 | Charges and refunds KSP makes part by part in one instant become one entry (summed change, last balance, `count`); credits and debits of the same reason stay separate | One line per instant | L2 | automated | `F18_` |
| F19 | Recovered debris, flags and space objects are not recorded (the same rule as for destruction) | debris and flags ignored | L3 | automated | `F19_` |
