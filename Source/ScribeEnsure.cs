// Scribe: makes sure the scenario module exists in the running game.
// AddToAllGames only covers new games; a save made before Scribe has no SCENARIO node, and in round 1-2 of 12/11/2026
// the module was not created on that path. This addon adds it when it is missing, in every scene Scribe works in.
using UnityEngine;

namespace Scribe
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public class ScribeEnsureKSC : ScribeEnsure { }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class ScribeEnsureFlight : ScribeEnsure { }

    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class ScribeEnsureEditor : ScribeEnsure { }

    [KSPAddon(KSPAddon.Startup.TrackingStation, false)]
    public class ScribeEnsureTracking : ScribeEnsure { }

    public class ScribeEnsure : MonoBehaviour
    {
        void Start()
        {
            if (ScribeModule.Instance != null) return;
            Game game = HighLogic.CurrentGame;
            if (game == null) return;
            foreach (ProtoScenarioModule p in game.scenarios)
                if (p.moduleName == typeof(ScribeModule).Name)
                {
                    // the node is in the save but the runner did not instantiate it: load it now
                    if (ScenarioRunner.Instance != null) p.Load(ScenarioRunner.Instance);
                    Debug.Log("[Scribe] module was in the save but not running; loaded it");
                    return;
                }
            ProtoScenarioModule proto = game.AddProtoScenarioModule(typeof(ScribeModule),
                GameScenes.SPACECENTER, GameScenes.FLIGHT, GameScenes.EDITOR, GameScenes.TRACKSTATION);
            if (ScenarioRunner.Instance != null) proto.Load(ScenarioRunner.Instance);
            Debug.Log("[Scribe] module was missing; added it to this game");
        }
    }
}
