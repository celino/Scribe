#!/usr/bin/env python3
"""Builds the synthetic career save used by the in-game rounds (no personal data, a few KB).

    python3 -I tests/ingame/make-save.py [--craft path/to/Some.craft] [--out tests/ingame/save]

The save is written from the template below, not cut out of a real save, so it is reproducible and carries
nothing from anybody's career: Normal difficulty, 100,000 funds, 50 reputation, 100 science, only the start
technology, every facility at its first level (lvl = 0), four stock kerbals, no vessels, no contracts yet (the game offers some
on the first visit to Mission Control). The Scribe scenario is NOT in it on purpose: scenario `menu` proves
that the module adds itself to a save that never had it; the other scenarios seed it (see run-ingame.sh).
The optional craft (a plain stock probe) is copied to Ships/VAB/Scribe Probe.craft.
"""
import argparse, pathlib, re, shutil

FACILITIES = ["LaunchPad", "Runway", "VehicleAssemblyBuilding", "SpaceplaneHangar", "TrackingStation",
              "AstronautComplex", "MissionControl", "ResearchAndDevelopment", "Administration", "FlagPole"]
KERBALS = [("Jebediah Kerman", "Male", "Pilot"), ("Bill Kerman", "Male", "Engineer"),
           ("Bob Kerman", "Male", "Scientist"), ("Valentina Kerman", "Female", "Pilot")]


def kerbal(i, name, gender, trait):
    return f"""\t\tKERBAL
\t\t{{
\t\t\tname = {name}
\t\t\tgender = {gender}
\t\t\ttype = Crew
\t\t\ttrait = {trait}
\t\t\tbrave = 0.5
\t\t\tdumb = 0.5
\t\t\tbadS = {'True' if trait == 'Engineer' else 'False'}
\t\t\tveteran = False
\t\t\ttour = False
\t\t\tstate = Available
\t\t\tinactive = False
\t\t\tinactiveTimeEnd = 0
\t\t\tgExperienced = 0
\t\t\toutDueToG = False
\t\t\tToD = 0
\t\t\tidx = {i}
\t\t\textraXP = 0
\t\t\thasHelmetOn = False
\t\t\thasNeckRingOn = True
\t\t\thasVisorDown = False
\t\t\tsuit = Default
\t\t\tcompletedFirstEVA = False
\t\t}}
"""


def build():
    fac = "".join(f"\t\tSpaceCenter/{f}\n\t\t{{\n\t\t\tlvl = 0\n\t\t}}\n" for f in FACILITIES)
    kerbals = "".join(kerbal(i, *k) for i, k in enumerate(KERBALS))
    return f"""GAME
{{
\tversion = 1.12.5
\tTitle = scribe-synthetic (CAREER)
\tDescription = Synthetic save for the Scribe test rounds.
\tMode = CAREER
\tStatus = 1
\tSeed = 1
\tscene = 5
\teditor = VAB
\tflag = Squad/Flags/default
\tlaunchID = 1
\tdefaultVABLaunchSite = LaunchPad
\tdefaultSPHLaunchSite = Runway
\tPARAMETERS
\t{{
\t\tpreset = Normal
\t\tFLIGHT
\t\t{{
\t\t\tCanQuickSave = True
\t\t\tCanQuickLoad = True
\t\t\tCanAutoSave = True
\t\t}}
\t\tCAREER
\t\t{{
\t\t\tTechTreeUrl = GameData/Squad/Resources/TechTree.cfg
\t\t}}
\t}}
\tSCENARIO
\t{{
\t\tname = Funding
\t\tscene = 7, 8, 5, 6
\t\tfunds = 100000
\t}}
\tSCENARIO
\t{{
\t\tname = Reputation
\t\tscene = 7, 8, 5, 6
\t\trep = 50
\t}}
\tSCENARIO
\t{{
\t\tname = ResearchAndDevelopment
\t\tscene = 7, 8, 5, 6
\t\tsci = 100
\t}}
\tSCENARIO
\t{{
\t\tname = ScenarioUpgradeableFacilities
\t\tscene = 5, 6, 7, 8
{fac}\t}}
\tSCENARIO
\t{{
\t\tname = ProgressTracking
\t\tscene = 6, 7, 8, 5
\t}}
\tSCENARIO
\t{{
\t\tname = ContractSystem
\t\tscene = 7, 8, 5, 6
\t}}
\tFLIGHTSTATE
\t{{
\t\tversion = 1.12.5
\t\tUT = 0
\t\tactiveVessel = 0
\t}}
\tROSTER
\t{{
{kerbals}\t}}
}}
"""


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--craft")
    ap.add_argument("--out", default=str(pathlib.Path(__file__).parent / "save"))
    a = ap.parse_args()
    out = pathlib.Path(a.out)
    shutil.rmtree(out, ignore_errors=True)
    (out / "Ships" / "VAB").mkdir(parents=True)
    (out / "persistent.sfs").write_text(build(), encoding="utf-8", newline="\n")
    if a.craft:
        txt = pathlib.Path(a.craft).read_text(encoding="utf-8")
        txt = re.sub(r"^ship = .*$", "ship = Scribe Probe", txt, count=1, flags=re.M)
        txt = txt.replace("FlagPack/Flags/Brazil", "Squad/Flags/default")  # stock flag: nothing personal, no mod needed
        (out / "Ships" / "VAB" / "Scribe Probe.craft").write_text(txt, encoding="utf-8", newline="\n")
    print("wrote", out)


if __name__ == "__main__":
    main()
