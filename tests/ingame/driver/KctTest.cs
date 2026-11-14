#if KCT
// KCT part of the Scribe rounds (13/11/2026): drives Kerbal Construction Time's own code (queue a vessel, finish it, queue and
// finish a tech, finish a facility upgrade) and logs what Scribe wrote. Only loaded in mode "kct" (references KerbalConstructionTime.dll).
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KerbalConstructionTime;
using Scribe;
using UnityEngine;

public static class KctTest
{
    static void Log(string m) { Rc.Log(m); }
    static int Mark;
    static void Since(string label)
    {
        var e = ScribeModule.Instance.Ledger.Entries;
        Log($"{label}: {e.Count - Mark} new ledger entries");
        for (int i = Mark; i < e.Count; i++)
            Log($"   + {e[i].Type} {e[i].Reason} delta={e[i].Delta:R} balance={e[i].Balance:R} text='{e[i].Text}' vessel='{e[i].Vessel}'");
        Mark = e.Count;
    }

    public static IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(20f);
        Rc.Snap("kct start");
        Log("scribe " + (ScribeModule.Instance == null ? "ABSENT" : "present") + "; KCT ActiveKSC " + (KCT_GameStates.ActiveKSC != null) + "; scene " + HighLogic.LoadedScene);
        if (ScribeModule.Instance == null || KCT_GameStates.ActiveKSC == null) { Log("cannot run; done"); Application.Quit(); yield break; }
        Mark = ScribeModule.Instance.Ledger.Entries.Count;

        // 1. vessel: queue and finish
        KCT_BuildListVessel blv = null;
        try
        {
            ShipConstruct ship = ShipConstruction.LoadShip(Path.Combine(KSPUtil.ApplicationRootPath, "Ships/VAB/Kerbal 1-5.craft"));
            Log("ship loaded: " + (ship == null ? "null" : ship.parts.Count + " parts"));
            blv = new KCT_BuildListVessel(ship, "LaunchPad", 500, 600, "Squad/Flags/default");
            blv.shipName = "Scribe KCT Test";
            blv.type = KCT_BuildListVessel.ListType.VAB;
            var added = KCT_Utilities.AddVesselToBuildList(blv);
            Log("AddVesselToBuildList -> " + (added == null ? "null" : added.shipName) + "; VABList " + KCT_GameStates.ActiveKSC.VABList.Count);
        }
        catch (Exception ex) { Log("vessel queue threw " + ex.GetType().Name + ": " + ex.Message); }
        yield return new WaitForSecondsRealtime(2f);
        Since("after queueing the vessel");
        try
        {
            if (blv != null)
            {
                blv.progress = blv.buildPoints + blv.integrationPoints;
                blv.IncrementProgress(0);
                Log($"after finishing: VABList {KCT_GameStates.ActiveKSC.VABList.Count} VABWarehouse {KCT_GameStates.ActiveKSC.VABWarehouse.Count}");
            }
        }
        catch (Exception ex) { Log("vessel finish threw " + ex.GetType().Name + ": " + ex.Message); }
        yield return new WaitForSecondsRealtime(2f);
        Since("after the vessel was built (KCT moved it to the warehouse)");

        // 2. tech: research (stock event, as the R&D button fires it) -> KCT queues -> finish
        try
        {
            string id = null; int cost = 0;
            foreach (var t in AssetBase.RnDTechTree.GetTreeTechs())
                if (t.scienceCost > 0 && t.scienceCost <= 40 && ResearchAndDevelopment.Instance.GetTechState(t.techID) != null) { id = t.techID; cost = t.scienceCost; break; }
            var proto = ResearchAndDevelopment.Instance.GetTechState(id);
            Log($"tech state before: {proto.state}; making it Unavailable for the test (test save only)");
            proto.state = RDTech.State.Unavailable;
            ResearchAndDevelopment.Instance.SetTechState(id, proto);
            Log("tech picked: " + id + " cost " + cost);
            var go = new GameObject("scribe-test-rdtech");
            var rd = go.AddComponent<RDTech>();
            rd.techID = id; rd.scienceCost = cost; rd.title = id; rd.partsPurchased = new List<AvailablePart>();
            ResearchAndDevelopment.Instance.AddScience(cost, TransactionReasons.None);
            GameEvents.OnTechnologyResearched.Fire(new GameEvents.HostTargetAction<RDTech, RDTech.OperationResult>(rd, RDTech.OperationResult.Successful));
            Log("TechList after the stock event: " + KCT_GameStates.TechList.Count + " (" + string.Join(",", KCT_GameStates.TechList.Select(x => x.techID).ToArray()) + ")");
            var item = KCT_GameStates.TechList.FirstOrDefault(x => x.techID == id);
            if (item == null) { item = new KCT_TechItem(rd); KCT_GameStates.TechList.Add(item); Log("KCT did not queue it; added by hand"); }
            Mark = Mark; // keep
        }
        catch (Exception ex) { Log("tech queue threw " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace); }
        yield return new WaitForSecondsRealtime(2f);
        Since("after researching a tech (KCT queued it)");
        try
        {
            var item = KCT_GameStates.TechList.FirstOrDefault();
            if (item != null) { item.progress = item.scienceCost; item.IncrementProgress(0); Log("after finishing: TechList " + KCT_GameStates.TechList.Count + ", node state " + ResearchAndDevelopment.Instance.GetTechState(item.techID).state); }
        }
        catch (Exception ex) { Log("tech finish threw " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace); }
        yield return new WaitForSecondsRealtime(2f);
        Since("after the tech was unlocked by KCT");

        // 3. facility upgrade: finish one
        try
        {
            string fid = null; int lvl = 0; string fname = null;
            foreach (var kv in ScenarioUpgradeableFacilities.protoUpgradeables)
            {
                var f = kv.Value.facilityRefs.FirstOrDefault();
                if (f != null && f.FacilityLevel < f.MaxLevel && !kv.Key.Contains("LaunchPad") && !kv.Key.Contains("Runway")) { fid = kv.Key; lvl = f.FacilityLevel; fname = f.name; break; }
            }
            Log($"facility picked: {fid} level {lvl}");
            if (fid != null)
            {
                float norm = (lvl + 1f) / Math.Max(1, ScenarioUpgradeableFacilities.protoUpgradeables[fid].facilityRefs[0].MaxLevel);
                var up = new KCT_UpgradingBuilding(null, fid, lvl + 1, lvl, fname);
                up.progress = 1; up.BP = 1;
                KCT_GameStates.ActiveKSC.KSCTech.Add(up);
                up.IncrementProgress(0);
                Log($"after finishing: level now {ScenarioUpgradeableFacilities.protoUpgradeables[fid].facilityRefs[0].FacilityLevel}; norm {ScenarioUpgradeableFacilities.GetFacilityLevel(fid)}");
            }
        }
        catch (Exception ex) { Log("facility threw " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace); }
        yield return new WaitForSecondsRealtime(3f);
        Since("after the facility upgrade finished");

        Rc.Dump();
        try { GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE); Log("saved"); }
        catch (Exception ex) { Log("save threw " + ex.GetType().Name); }
        yield return new WaitForSecondsRealtime(3f);
        Log("done");
        yield return new WaitForSecondsRealtime(2f);
        Application.Quit();
    }
}
#endif
