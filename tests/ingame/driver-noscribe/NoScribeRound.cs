// Test-only addon for the "noscribe" round: the Scribe folder is removed from GameData and the save still holds a
// ledger. The game must load to the Space Center, and saving there must keep the ledger node untouched.
// This project does not reference Scribe.dll on purpose: it has to load without it.
using System;
using System.Collections;
using System.IO;
using UnityEngine;

static class Nc
{
    public static void Log(string m) => Debug.Log("[ScribeRound] " + m);
    public static void Assert(string id, bool ok, string detail) => Log("ASSERT " + id + " " + (ok ? "PASS" : "FAIL") + " " + detail);
}

[KSPAddon(KSPAddon.Startup.MainMenu, false)]
public class NoScribeLoader : MonoBehaviour
{
    static bool ran;
    void Start()
    {
        if (ran) return;
        ran = true;
        StartCoroutine(Go());
    }
    IEnumerator Go()
    {
        yield return new WaitForSecondsRealtime(3f);
        ConfigNode cfg = ConfigNode.Load(Path.Combine(KSPUtil.ApplicationRootPath, "GameData/zzScribeRound/scenario.cfg"))?.GetNode("SCRIBEROUND");
        string folder = cfg == null ? "scribe-test" : cfg.GetValue("save");
        Game game = GamePersistence.LoadGame("persistent", folder, true, false);
        if (game == null) { Nc.Log("could not load save " + folder + "; done"); Application.Quit(); yield break; }
        GamePersistence.UpdateScenarioModules(game);
        game.startScene = GameScenes.SPACECENTER;
        HighLogic.CurrentGame = game;
        HighLogic.SaveFolder = folder;
        game.Start();
    }
}

[KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
public class NoScribeCenter : MonoBehaviour
{
    void Start() { StartCoroutine(Run()); }
    IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(15f);
        bool noScribe = AssemblyLoader.GetClassByName(typeof(ScenarioModule), "ScribeModule") == null;
        Nc.Assert("F14", HighLogic.LoadedScene == GameScenes.SPACECENTER && noScribe, "Space Center reached; ScribeModule class present: " + !noScribe);
        Funding.Instance.AddFunds(10, TransactionReasons.Cheating); // a change the missing module cannot record
        GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
        string sfs = File.ReadAllText(Path.Combine(KSPUtil.ApplicationRootPath, "saves/" + HighLogic.SaveFolder + "/persistent.sfs"));
        Nc.Assert("F14", sfs.Contains("name = ScribeModule") && sfs.Contains("text = Jebediah Kerman"), "the ledger node is still in the saved file");
        yield return new WaitForSecondsRealtime(3f);
        Nc.Log("done");
        yield return new WaitForSecondsRealtime(2f);
        Application.Quit();
    }
}
