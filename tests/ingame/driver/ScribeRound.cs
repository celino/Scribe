// Test-only addon for Scribe rounds. Each round prints "[ScribeRound] ASSERT Fnn PASS|FAIL detail" lines that
// tests/ingame/check_round.py turns into the exit status.
// Main menu -> loads the save named in GameData/zzScribeRound/scenario.cfg
// -> Space Center -> runs the mode: tx (8 known transactions) or revert (save, more transactions, reload, check).
// Log lines start with [ScribeRound]; the last one is "done".
using System;
using System.Collections;
using System.IO;
using System.Linq;
using KSP.UI.Screens;
using Scribe;
using UnityEngine;

static class Rc
{
    public static ConfigNode Load()
    {
        string path = Path.Combine(KSPUtil.ApplicationRootPath, "GameData/zzScribeRound/scenario.cfg");
        return File.Exists(path) ? ConfigNode.Load(path)?.GetNode("SCRIBEROUND") : null;
    }
    public static void Log(string m) => Debug.Log("[ScribeRound] " + m);
    public static int stage = 1;

    public static void Snap(string label) =>
        Log($"{label}: funds {Funding.Instance.Funds:F1} rep {Reputation.Instance.reputation:F2} sci {ResearchAndDevelopment.Instance.Science:F2} UT {Planetarium.GetUniversalTime():F0} entries {Count()}");

    public static void Assert(string id, bool ok, string detail) =>
        Log("ASSERT " + id + " " + (ok ? "PASS" : "FAIL") + " " + detail);

    // F01: every balance entry's balance equals the previous balance of the same kind plus its delta
    public static bool BalancesChain(out string why)
    {
        why = "";
        var last = new System.Collections.Generic.Dictionary<string, double>();
        foreach (var e in ScribeModule.Instance.Ledger.Entries.Where(x => x.IsBalance))
        {
            double prev;
            if (last.TryGetValue(e.Type, out prev) && Math.Abs(prev + e.Delta - e.Balance) > 0.05)
            { why = $"{e.Type} {e.Reason}: {prev} + {e.Delta} != {e.Balance}"; return false; }
            last[e.Type] = e.Balance;
        }
        return true;
    }

    public static double[] Deltas(string type) =>
        ScribeModule.Instance.Ledger.Entries.Where(e => e.Type == type).Select(e => e.Delta).ToArray();

    public static bool Near(double[] got, params double[] want) =>
        got.Length == want.Length && got.Zip(want, (a, b) => Math.Abs(a - b) < 0.01).All(x => x);

    public static int Count() => ScribeModule.Instance == null ? -1 : ScribeModule.Instance.Ledger.Entries.Count;

    public static void Protos(string label)
    {
        foreach (var p in HighLogic.CurrentGame.scenarios)
            if (p.moduleName == "ScribeModule" || p.moduleName == "ELSettings" || p.moduleName == "ContractSystem")
                Log($"{label} proto {p.moduleName} scenes [{string.Join(",", p.targetScenes.Select(x => x.ToString()).ToArray())}] moduleRef {(p.moduleRef == null ? "null" : "set")}");
        Log("class lookup: " + (AssemblyLoader.GetClassByName(typeof(ScenarioModule), "ScribeModule") != null) + "; scene " + HighLogic.LoadedScene);
    }

    public static void Dump()
    {
        if (ScribeModule.Instance == null) { Log("ScribeModule.Instance is null"); return; }
        foreach (var e in ScribeModule.Instance.Ledger.Entries)
            Log($"ENTRY ut={e.Ut:F0} {e.Type} {e.Reason} delta={e.Delta:R} balance={e.Balance:R} vessel='{e.Vessel}' body='{e.Body}' text='{e.Text}'");
    }

    public static void EnsureScenario(Game game)
    {
        if (game.scenarios.Exists(x => x.moduleName == nameof(ScribeModule))) { Log("Scribe scenario already in the save"); return; }
        game.AddProtoScenarioModule(typeof(ScribeModule), GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION, GameScenes.EDITOR);
        Log("Scribe scenario added by hand (it was not in the save)");
    }
}

[KSPAddon(KSPAddon.Startup.MainMenu, false)]
public class RoundLoader : MonoBehaviour
{
    static bool ran;
    void Start()
    {
        ConfigNode cfg = Rc.Load();
        if (cfg == null || ran) return;
        ran = true;
        StartCoroutine(Go(cfg));
    }
    IEnumerator Go(ConfigNode cfg)
    {
        yield return new WaitForSecondsRealtime(3f);
        string folder = cfg.GetValue("save");
        Game game = GamePersistence.LoadGame("persistent", folder, true, false);
        if (game == null) { Rc.Log("could not load save " + folder + "; done"); Application.Quit(); yield break; }
        Rc.Log("loaded save " + folder);
        GamePersistence.UpdateScenarioModules(game);
        if ((cfg.GetValue("mode") ?? "") != "menu") Rc.EnsureScenario(game); // menu: the module must add itself
        game.startScene = GameScenes.SPACECENTER;
        HighLogic.CurrentGame = game;
        HighLogic.SaveFolder = folder;
        game.Start();
    }
}

[KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
public class RoundCenter : MonoBehaviour
{
    void Start()
    {
        ConfigNode cfg = Rc.Load();
        if (cfg == null) return;
        string mode = cfg.GetValue("mode") ?? "tx";
        int stage = Rc.stage;
        Rc.stage++;
        #if KCT
        if (mode == "kct") { StartCoroutine(KctTest.Run()); return; }
#endif
        if (mode == "dock") { StartCoroutine(DockCenter()); return; }
        if (mode == "menu") { StartCoroutine(Menu()); return; }
        if (mode == "bench") { StartCoroutine(Bench()); return; }
        if (mode == "flight") { StartCoroutine(FlightCenter(stage)); return; }
        if (stage == 1) StartCoroutine(mode == "revert" ? RevertA(cfg) : Tx());
        else if (stage == 2) StartCoroutine(RevertB(cfg));
    }

    static long FileSize() { return new FileInfo(Path.Combine(KSPUtil.ApplicationRootPath, "saves/" + HighLogic.SaveFolder + "/persistent.sfs")).Length; }

    // bench: cost per event with Scribe unhooked (baseline) and hooked, save time and file size, 6000 events to pass the 5000 cap
    IEnumerator Bench()
    {
        yield return Wait(15f);
        Rc.Snap("bench start");
        Rc.Log("KCT modules: " + string.Join(", ", ScenarioRunner.GetLoadedModules().Select(m => m.GetType().Name).Where(n => n.IndexOf("KCT", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Construction", StringComparison.OrdinalIgnoreCase) >= 0).ToArray()) + "; KCT assembly loaded: " + AssemblyLoader.loadedAssemblies.Any(a => a.name.IndexOf("KerbalConstructionTime", StringComparison.OrdinalIgnoreCase) >= 0));
        var inst = ScribeModule.Instance;
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var unhook = typeof(ScribeModule).GetMethod("Unhook", flags); var hook = typeof(ScribeModule).GetMethod("Hook", flags);
        const int N = 6000;
        unhook.Invoke(inst, null);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < N; i++) Funding.Instance.AddFunds(i % 2 == 0 ? 1 : -1, TransactionReasons.Cheating);
        sw.Stop(); double baseMs = sw.Elapsed.TotalMilliseconds;
        Rc.Log($"baseline (Scribe unhooked): {N} events {baseMs:F1} ms = {baseMs / N * 1000:F2} us/event");
        sw.Restart(); GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE); sw.Stop();
        Rc.Log($"save without new entries: {sw.Elapsed.TotalMilliseconds:F0} ms, persistent.sfs {FileSize()} bytes, entries {Rc.Count()}");
        hook.Invoke(inst, null);
        sw.Restart();
        for (int i = 0; i < N; i++) Funding.Instance.AddFunds(i % 2 == 0 ? 1 : -1, TransactionReasons.Cheating);
        sw.Stop(); double hookMs = sw.Elapsed.TotalMilliseconds;
        Rc.Log($"hooked: {N} events {hookMs:F1} ms = {hookMs / N * 1000:F2} us/event (extra {(hookMs - baseMs) / N * 1000:F2} us/event), entries {Rc.Count()} (cap {inst.Ledger.Cap})");
        sw.Restart(); GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE); sw.Stop();
        Rc.Log($"save with {Rc.Count()} entries: {sw.Elapsed.TotalMilliseconds:F0} ms, persistent.sfs {FileSize()} bytes");
        double extraUs = (hookMs - baseMs) / N * 1000;
        Rc.Assert("F13", extraUs < 10, $"extra cost {extraUs:F2} us/event (limit 10; about 2 expected)");
        Rc.Assert("F03", Rc.Count() <= inst.Ledger.Cap, $"entries {Rc.Count()} within the cap of {inst.Ledger.Cap} after {2 * N} events");
        yield return Wait(3f);
        Rc.Log("done");
        yield return Wait(0.2f);
        Application.Quit();
    }

    IEnumerator DockCenter()
    {
        yield return Wait(15f);
        Rc.Snap("dock start");
        Launch();
    }

    // menu: the save has no SCRIBE node and the driver does not add one; Scribe must add itself at the Space Center
    IEnumerator Menu()
    {
        yield return Wait(15f);
        Rc.Snap("menu start");
        Rc.Assert("F12", ScribeModule.Instance != null, "module running without the driver adding it: " + (ScribeModule.Instance != null));
        if (ScribeModule.Instance != null)
        {
            Funding.Instance.AddFunds(1234, TransactionReasons.Cheating);
            yield return Wait(1f);
            var fd = Rc.Deltas("funds");
            Rc.Assert("F12", fd.Length == 1 && Math.Abs(fd[0] - 1234) < 0.01, "first change recorded: " + string.Join(",", fd.Select(d => d.ToString("R")).ToArray()));
            GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
            string sfs = File.ReadAllText(Path.Combine(KSPUtil.ApplicationRootPath, "saves/" + HighLogic.SaveFolder + "/persistent.sfs"));
            Rc.Assert("F12", sfs.Contains("name = ScribeModule"), "the save now has the SCRIBE scenario node");
        }
        yield return Wait(3f);
        Rc.Log("done");
        yield return Wait(2f);
        Application.Quit();
    }

    public static void Launch()
    {
        string craft = Path.Combine(KSPUtil.ApplicationRootPath, "saves/" + HighLogic.SaveFolder + "/Ships/VAB/" + (System.IO.File.Exists(Path.Combine(KSPUtil.ApplicationRootPath, "saves/" + HighLogic.SaveFolder + "/Ships/VAB/Scribe Probe.craft")) ? "Scribe Probe" : "[GetScience]R") + ".craft");
        Rc.Log("launching " + craft);
        FlightDriver.StartWithNewLaunch(craft, HighLogic.CurrentGame.flagURL, "LaunchPad", new VesselCrewManifest());
    }

    // flight: stage 1 = kerbal death + contract at the KSC, then launch (flight 1: destroy);
    //         stage 2 = launch again (flight 2: recover); stage 3 = dump, save, done
    IEnumerator FlightCenter(int stage)
    {
        yield return Wait(15f);
        Rc.Snap("KSC stage " + stage);
        if (stage == 1)
        {
            var k = HighLogic.CurrentGame.CrewRoster.Crew.FirstOrDefault(c => c.rosterStatus == ProtoCrewMember.RosterStatus.Available);
            if (k != null) { Rc.Log("killing " + k.name); k.rosterStatus = ProtoCrewMember.RosterStatus.Dead; }
            else Rc.Log("no available kerbal");
            yield return Wait(0.2f);
            var cs = Contracts.ContractSystem.Instance;
            Rc.Log("contracts: " + (cs == null ? "no system" : string.Join(", ", cs.Contracts.GroupBy(x => x.ContractState.ToString()).Select(g => g.Key + " " + g.Count()).ToArray())));
            var c = Contracts.ContractSystem.Instance.Contracts.FirstOrDefault(x => x.ContractState == Contracts.Contract.State.Offered)
                ?? Contracts.ContractSystem.Instance.Contracts.FirstOrDefault(x => x.ContractState == Contracts.Contract.State.Active);
            if (c != null)
            {
                Rc.Log("contract: " + c.Title);
                if (c.ContractState == Contracts.Contract.State.Offered) { c.Accept(); Rc.Snap("accepted"); yield return Wait(0.2f); }
                c.Complete(); Rc.Snap("completed"); yield return Wait(0.2f);
            }
            else Rc.Log("no offered contract");
            Launch();
        }
        else if (stage == 2) Launch();
        else
        {
            Rc.Dump();
            var all = ScribeModule.Instance.Ledger.Entries;
            Func<string, bool> named = t => all.Any(e => e.Type == t && !string.IsNullOrEmpty(e.Vessel) && e.Vessel.Length > 0);
            Rc.Assert("F08", named("launch") && named("destroyed") && named("recovered"),
                "launch/destroyed/recovered entries with a vessel name: " + named("launch") + "/" + named("destroyed") + "/" + named("recovered"));
            Rc.Assert("F10", all.Any(e => e.Type == "kerbal" && e.Text.Length > 0), "kerbal entry for the death");
            Rc.Assert("F10", all.Any(e => e.Type == "contract"), "contract entry (needs a contract offer; scenario contracts)");
            GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
            Rc.Log("saved");
            yield return Wait(3f);
            Rc.Log("done");
            yield return Wait(0.2f);
            Application.Quit();
        }
    }

    static IEnumerator Wait(float s) { yield return new WaitForSecondsRealtime(s); }

    IEnumerator Tx()
    {
        yield return Wait(15f);
        Rc.Snap("start");
        Rc.Protos("tx");
        Rc.Log("modules: " + string.Join(", ", ScenarioRunner.GetLoadedModules().Select(m => m.GetType().Name).ToArray()));
        Funding.Instance.AddFunds(5000, TransactionReasons.ContractReward); Rc.Snap("after +5000 ContractReward");
        yield return Wait(0.2f);
        Funding.Instance.AddFunds(-2000, TransactionReasons.RnDPartPurchase); Rc.Snap("after -2000 RnDPartPurchase");
        yield return Wait(0.2f);
        Funding.Instance.AddFunds(-300, TransactionReasons.CrewRecruited); Rc.Snap("after -300 CrewRecruited");
        yield return Wait(0.2f);
        Funding.Instance.AddFunds(750, TransactionReasons.VesselRecovery); Rc.Snap("after +750 VesselRecovery");
        yield return Wait(0.2f);
        Reputation.Instance.AddReputation(12, TransactionReasons.ContractReward); Rc.Snap("after +12 rep ContractReward");
        yield return Wait(0.2f);
        Reputation.Instance.AddReputation(-5, TransactionReasons.ContractPenalty); Rc.Snap("after -5 rep ContractPenalty");
        yield return Wait(0.2f);
        ResearchAndDevelopment.Instance.AddScience(20, TransactionReasons.ScienceTransmission); Rc.Snap("after +20 sci ScienceTransmission");
        yield return Wait(0.2f);
        ResearchAndDevelopment.Instance.AddScience(-8, TransactionReasons.RnDTechResearch); Rc.Snap("after -8 sci RnDTechResearch");
        yield return Wait(3f);
        Rc.Dump();
        string why;
        Rc.Assert("F01", Rc.Count() == 8 && Rc.Near(Rc.Deltas("funds"), 5000, -2000, -300, 750) && Rc.Near(Rc.Deltas("science"), 20, -8),
            "funds " + string.Join(",", Rc.Deltas("funds").Select(d => d.ToString("R")).ToArray()) + "; science " + string.Join(",", Rc.Deltas("science").Select(d => d.ToString("R")).ToArray()) + "; entries " + Rc.Count());
        var rep = Rc.Deltas("reputation");
        Rc.Assert("F01", rep.Length == 2 && rep[0] > 0 && rep[1] < 0, "reputation deltas " + string.Join(",", rep.Select(d => d.ToString("R")).ToArray()));
        Rc.Assert("F01", Rc.BalancesChain(out why), "each balance = previous balance + delta " + why);
        GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
        Rc.Log("saved");
        yield return Wait(3f);
        Rc.Log("done");
        yield return Wait(0.2f);
        Application.Quit();
    }

    // revert: A (2 transactions) -> quicksave -> B (2 more) -> reload quicksave -> stage 2 checks that B is gone
    IEnumerator RevertA(ConfigNode cfg)
    {
        yield return Wait(15f);
        Rc.Snap("start");
        Funding.Instance.AddFunds(5000, TransactionReasons.ContractReward); Rc.Snap("A1 +5000 ContractReward");
        yield return Wait(1f);
        Funding.Instance.AddFunds(-2000, TransactionReasons.RnDPartPurchase); Rc.Snap("A2 -2000 RnDPartPurchase");
        yield return Wait(1f);
        GamePersistence.SaveGame("quicksave", HighLogic.SaveFolder, SaveMode.OVERWRITE);
        Rc.Log("quicksave written");
        yield return Wait(1f);
        Funding.Instance.AddFunds(750, TransactionReasons.VesselRecovery); Rc.Snap("B1 +750 VesselRecovery (to be reverted)");
        yield return Wait(1f);
        ResearchAndDevelopment.Instance.AddScience(20, TransactionReasons.ScienceTransmission); Rc.Snap("B2 +20 sci (to be reverted)");
        yield return Wait(1f);
        Rc.Dump();
        Game game = GamePersistence.LoadGame("quicksave", HighLogic.SaveFolder, true, false);
        if (game == null) { Rc.Log("quickload failed; done"); Application.Quit(); yield break; }
        GamePersistence.UpdateScenarioModules(game);
        // like the stock quickload: the loaded game becomes persistent.sfs, then the scene is loaded again
        HighLogic.CurrentGame = game;
        GamePersistence.SaveGame(game, "persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
        Rc.Log("quickload started (persistent.sfs replaced by the quicksave)");
        HighLogic.LoadScene(GameScenes.SPACECENTER);
    }

    IEnumerator RevertB(ConfigNode cfg)
    {
        yield return Wait(15f);
        Rc.Snap("after quickload (expect entries 2, funds = after A2)");
        Rc.Dump();
        Rc.Assert("F07", Rc.Count() == 2 && Rc.Near(Rc.Deltas("funds"), 5000, -2000) && Rc.Deltas("science").Length == 0,
            "after quickload: entries " + Rc.Count() + " (the two entries made after the quicksave are gone)");
        Funding.Instance.AddFunds(-300, TransactionReasons.CrewRecruited); Rc.Snap("C -300 CrewRecruited (delta must be exactly -300)");
        yield return Wait(0.2f);
        Rc.Dump();
        var fd = Rc.Deltas("funds");
        Rc.Assert("F07", fd.Length == 3 && Math.Abs(fd[2] + 300) < 0.01, "first change after the quickload is exactly -300 (balances came back with the save): " + (fd.Length == 0 ? "none" : fd[fd.Length - 1].ToString("R")));
        GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
        Rc.Log("saved");
        yield return Wait(3f);
        Rc.Log("done");
        yield return Wait(0.2f);
        Application.Quit();
    }
}

[KSPAddon(KSPAddon.Startup.Flight, false)]
public class RoundFlight : MonoBehaviour
{
    static int flights;
    void Start()
    {
        ConfigNode cfg = Rc.Load();
        if (cfg == null) return;
        string m = cfg.GetValue("mode") ?? "";
        if (m == "dock") { StartCoroutine(Dock()); return; }
        if (m != "flight") return;
        flights++;
        StartCoroutine(Go(flights));
    }
    static string Vessels() => string.Join("; ", FlightGlobals.Vessels.Where(x => x.vesselType != VesselType.SpaceObject && x.vesselType != VesselType.Debris)
        .Select(x => $"'{x.vesselName}' {x.vesselType} parts {x.parts.Count} state {x.state} loaded {x.loaded}").ToArray());

    // dock: a second copy of the probe is spawned next to the first one and the two are coupled with Part.Couple, the call
    // behind a docking-port capture. The merged vessel is destroyed by the game: Scribe must not write "destroyed" for it.
    IEnumerator Dock()
    {
        yield return new WaitForSecondsRealtime(15f);
        Vessel a = FlightGlobals.ActiveVessel;
        Rc.Log($"dock: active '{(a == null ? "none" : a.vesselName)}' parts {(a == null ? 0 : a.parts.Count)} entries {Rc.Count()}");
        if (a == null || ScribeModule.Instance == null) { Rc.Log("nothing to do; done"); Application.Quit(); yield break; }
        int destroyEvents = 0, coupleEvents = 0;
        GameEvents.onVesselDestroy.Add(x => { destroyEvents++; Rc.Log($"EVENT onVesselDestroy '{x.vesselName}' type {x.vesselType} parts {x.parts.Count} state {x.state}"); });
        GameEvents.onPartCouple.Add(x => { coupleEvents++; Rc.Log($"EVENT onPartCouple {x.from.partInfo.name} -> {x.to.partInfo.name}"); });
        int before = ScribeModule.Instance.Ledger.Entries.Count(e => e.Type == "destroyed");
        string craft = Path.Combine(KSPUtil.ApplicationRootPath, "saves/" + HighLogic.SaveFolder + "/Ships/VAB/Scribe Probe.craft");
        ShipConstruct ship = ShipConstruction.LoadShip(craft);
        VesselCrewManifest crew = new VesselCrewManifest();
        Vessel b = null;
        var known = new System.Collections.Generic.HashSet<Vessel>(FlightGlobals.Vessels);
        try
        {
            ShipConstruction.AssembleForLaunch(ship, "LaunchPad", "Scribe Dock Partner", HighLogic.CurrentGame.flagURL, HighLogic.CurrentGame, crew);
            b = FlightGlobals.Vessels.FirstOrDefault(v => !known.Contains(v));
        }
        catch (Exception ex) { Rc.Log("spawn threw " + ex.GetType().Name + ": " + ex.Message); }
        Rc.Log("partner spawned: " + (b == null ? "no" : $"'{b.vesselName}' parts {b.parts.Count}"));
        bool spawned = b != null && b != a; // Vessel is a MonoBehaviour: after the merge it compares equal to null
        yield return new WaitForSecondsRealtime(3f);
        if (spawned)
        {
            Rc.Log($"coupling: {Vessels()}");
            try { b.rootPart.Couple(a.rootPart); }
            catch (Exception ex) { Rc.Log("couple threw " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace); }
            yield return new WaitForSecondsRealtime(5f);
            Rc.Log($"after coupling: {Vessels()}; couple events {coupleEvents}, destroy events {destroyEvents}");
        }
        int after = ScribeModule.Instance.Ledger.Entries.Count(e => e.Type == "destroyed");
        Rc.Dump();
        Rc.Assert("F09", spawned && coupleEvents >= 1 && destroyEvents >= 1, $"two vessels were coupled for real: partner {spawned}, couple events {coupleEvents}, destroy events {destroyEvents}");
        Rc.Assert("F09", after == before, $"no 'destroyed' entry for the merged vessel: {before} before, {after} after");
        Rc.Log("done");
        yield return new WaitForSecondsRealtime(2f);
        Application.Quit();
    }

    IEnumerator Go(int n)
    {
        yield return new WaitForSecondsRealtime(12f);
        Vessel v = FlightGlobals.ActiveVessel;
        Rc.Log($"flight {n}: active '{(v == null ? "none" : v.vesselName)}' situation {(v == null ? "-" : v.situation.ToString())} entries {Rc.Count()}");
        if (v == null) { Rc.Log("no vessel; done"); Application.Quit(); yield break; }
        if (n == 1)
        {
            StageManager.ActivateNextStage(); // first stage on the pad = liftoff, what fires onLaunch
            yield return new WaitForSecondsRealtime(3f);
            Rc.Log("destroying " + v.vesselName);
            v.Die();
            yield return new WaitForSecondsRealtime(5f);
            Rc.Snap("after Die");
            Rc.Dump();
            GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE); // the stock "return to KSC" saves; a bare LoadScene does not
            HighLogic.LoadScene(GameScenes.SPACECENTER);
        }
        else
        {
            Rc.Log("recovering " + v.vesselName);
            GameEvents.OnVesselRecoveryRequested.Fire(v);
            yield return new WaitForSecondsRealtime(10f);
            Rc.Log("still in scene " + HighLogic.LoadedScene);
        }
    }
}
