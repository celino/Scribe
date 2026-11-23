// Scribe: the KSP side. Hooks game events and keeps the ledger in the save (persistent.sfs, node SCRIBE).
// Reacts to events only; nothing here runs per frame.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Contracts;
using UnityEngine;

namespace Scribe
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames,
        GameScenes.SPACECENTER, GameScenes.FLIGHT, GameScenes.EDITOR, GameScenes.TRACKSTATION)]
    public class ScribeModule : ScenarioModule
    {
        const string Funds = "funds", Science = "science", Rep = "reputation";

        public static ScribeModule Instance;
        public Ledger Ledger = new Ledger();
        bool hooked;
        static readonly DestroyFilter destroyFilter = new DestroyFilter();
        static readonly FacilityDebounce facilityDebounce = new FacilityDebounce();

        public override void OnAwake()
        {
            base.OnAwake();
            Instance = this;
            Hook();
        }

        void OnDestroy()
        {
            Unhook();
            if (Instance == this) Instance = null;
        }

        void Hook()
        {
            if (hooked) return;
            hooked = true;
            GameEvents.OnFundsChanged.Add(OnFunds);
            GameEvents.OnScienceChanged.Add(OnScience);
            GameEvents.OnReputationChanged.Add(OnReputation);
            GameEvents.onLaunch.Add(OnLaunch);
            GameEvents.onVesselRecovered.Add(OnRecovered);
            GameEvents.onVesselWillDestroy.Add(OnVesselDestroy);
            GameEvents.onVesselDestroy.Add(OnVesselDestroy);
            GameEvents.onKerbalStatusChange.Add(OnKerbalStatus);
            GameEvents.onKerbalAdded.Add(OnKerbalAdded);
            GameEvents.onPartCouple.Add(OnCouple);
            GameEvents.OnTechnologyResearched.Add(OnTech);
            GameEvents.OnKSCFacilityUpgraded.Add(OnFacilityUpgraded);
            StartCoroutine(HookKct());
            GameEvents.Contract.onAccepted.Add(OnContractAccepted);
            GameEvents.Contract.onCompleted.Add(OnContractCompleted);
            GameEvents.Contract.onFailed.Add(OnContractFailed);
            GameEvents.Contract.onCancelled.Add(OnContractCancelled);
        }

        void Unhook()
        {
            if (!hooked) return;
            hooked = false;
            GameEvents.OnFundsChanged.Remove(OnFunds);
            GameEvents.OnScienceChanged.Remove(OnScience);
            GameEvents.OnReputationChanged.Remove(OnReputation);
            GameEvents.onLaunch.Remove(OnLaunch);
            GameEvents.onVesselRecovered.Remove(OnRecovered);
            GameEvents.onVesselWillDestroy.Remove(OnVesselDestroy);
            GameEvents.onVesselDestroy.Remove(OnVesselDestroy);
            GameEvents.onKerbalStatusChange.Remove(OnKerbalStatus);
            GameEvents.onKerbalAdded.Remove(OnKerbalAdded);
            GameEvents.onPartCouple.Remove(OnCouple);
            GameEvents.OnTechnologyResearched.Remove(OnTech);
            GameEvents.OnKSCFacilityUpgraded.Remove(OnFacilityUpgraded);
            if (kctTechDone != null) { kctTechDone.Remove(OnKctTechCompleted); kctTechDone = null; }
            GameEvents.Contract.onAccepted.Remove(OnContractAccepted);
            GameEvents.Contract.onCompleted.Remove(OnContractCompleted);
            GameEvents.Contract.onFailed.Remove(OnContractFailed);
            GameEvents.Contract.onCancelled.Remove(OnContractCancelled);
        }

        // ---- save / load ----

        public override void OnLoad(ConfigNode node)
        {
            Ledger = LedgerStore.Load(ToSaveNode(node));
            StartCoroutine(SeedBalances());
        }

        public override void OnSave(ConfigNode node)
        {
            var tree = new SaveNode();
            LedgerStore.Save(Ledger, tree);
            FromSaveNode(tree, node);
        }

        static SaveNode ToSaveNode(ConfigNode node)
        {
            var n = new SaveNode();
            foreach (ConfigNode.Value v in node.values) n.AddValue(v.name, v.value);
            foreach (ConfigNode c in node.GetNodes()) n.Nodes.Add(new KeyValuePair<string, SaveNode>(c.name, ToSaveNode(c)));
            return n;
        }

        static void FromSaveNode(SaveNode from, ConfigNode to)
        {
            foreach (var v in from.Values) to.AddValue(v.Key, v.Value);
            foreach (var c in from.Nodes) FromSaveNode(c.Value, to.AddNode(c.Key));
        }

        // One frame after loading: for balances the save did not carry (first run with this mod), start from the live value.
        IEnumerator SeedBalances()
        {
            yield return null;
            if (Funding.Instance != null && !Ledger.HasBalance(Funds)) Ledger.SetBalance(Funds, Funding.Instance.Funds);
            if (ResearchAndDevelopment.Instance != null && !Ledger.HasBalance(Science)) Ledger.SetBalance(Science, ResearchAndDevelopment.Instance.Science);
            if (Reputation.Instance != null && !Ledger.HasBalance(Rep)) Ledger.SetBalance(Rep, Reputation.Instance.reputation);
        }

        // ---- helpers ----

        static double Now()
        {
            return Planetarium.GetUniversalTime();
        }

        static string ActiveVessel()
        {
            Vessel v = HighLogic.LoadedSceneIsFlight ? FlightGlobals.ActiveVessel : null;
            return v != null ? v.vesselName : "";
        }

        static string ActiveBody()
        {
            Vessel v = HighLogic.LoadedSceneIsFlight ? FlightGlobals.ActiveVessel : null;
            return v != null && v.mainBody != null ? v.mainBody.bodyName : "";
        }

        void Balance(string kind, double value, TransactionReasons reason)
        {
            try { Ledger.RecordBalance(kind, value, Now(), reason.ToString(), ActiveVessel(), ActiveBody()); }
            catch (Exception ex) { Debug.LogError("[Scribe] " + kind + ": " + ex); }
        }

        void Event(string type, string reason, string vessel, string body, string text)
        {
            try { Ledger.RecordEvent(type, reason, Now(), vessel, body, text); }
            catch (Exception ex) { Debug.LogError("[Scribe] " + type + ": " + ex); }
        }

        // ---- events ----

        void OnFunds(double balance, TransactionReasons reason) { Balance(Funds, balance, reason); }
        void OnScience(float balance, TransactionReasons reason) { Balance(Science, balance, reason); }
        void OnReputation(float balance, TransactionReasons reason) { Balance(Rep, balance, reason); }

        void OnLaunch(EventReport report)
        {
            Event("launch", "Launch", ActiveVessel(), ActiveBody(), "");
        }

        void OnRecovered(ProtoVessel pv, bool quick)
        {
            if (pv == null || !ScribeRules.IsRecordableVessel(pv.vesselType.ToString())) return;
            Event("recovered", quick ? "RecoveredQuick" : "Recovered", pv.vesselName,
                pv.orbitSnapShot != null && pv.orbitSnapShot.ReferenceBodyIndex >= 0 ? BodyName(pv.orbitSnapShot.ReferenceBodyIndex) : "", "");
        }

        static string BodyName(int index)
        {
            CelestialBody b = FlightGlobals.Bodies != null && index < FlightGlobals.Bodies.Count ? FlightGlobals.Bodies[index] : null;
            return b != null ? b.bodyName : "";
        }

        void OnCouple(GameEvents.FromToAction<Part, Part> a)
        {
            float t = Time.realtimeSinceStartup;
            if (a.from != null && a.from.vessel != null) destroyFilter.NoteCoupled(a.from.vessel.id, t);
            if (a.to != null && a.to.vessel != null) destroyFilter.NoteCoupled(a.to.vessel.id, t);
        }

        void OnVesselDestroy(Vessel v)
        {
            if (v == null) return;
            if (!destroyFilter.ShouldRecord(v.id, v.vesselType.ToString(), v.parts != null ? v.parts.Count : 0, Time.realtimeSinceStartup)) return;
            Event("destroyed", "VesselDestroy", v.vesselName, v.mainBody != null ? v.mainBody.bodyName : "", "");
        }

        void OnKerbalStatus(ProtoCrewMember k, ProtoCrewMember.RosterStatus from, ProtoCrewMember.RosterStatus to)
        {
            if (k == null) return;
            string reason = ScribeRules.KerbalReason(to.ToString());
            if (reason == null) return;
            Event("kerbal", reason, ActiveVessel(), ActiveBody(), k.name);
        }

        void OnKerbalAdded(ProtoCrewMember k)
        {
            if (k != null) Event("kerbal", "Added", "", "", k.name);
        }

        void OnTech(GameEvents.HostTargetAction<RDTech, RDTech.OperationResult> a)
        {
            if (a.target != RDTech.OperationResult.Successful || a.host == null) return;
            Event("tech", "TechResearched", "", "", a.host.techID);
        }

        // Stock event: fires when a facility level is really applied (Kerbal Construction Time applies it when the wait ends).
        // The game can fire it once per facility object (the Astronaut Complex test fired it twice); the same text within a second is one upgrade.
        void OnFacilityUpgraded(Upgradeables.UpgradeableFacility f, int level)
        {
            if (f == null) return;
            string text = ScribeRules.FacilityText(f.id, level);
            if (!facilityDebounce.Accept(text, Time.realtimeSinceStartup)) return;
            Event("facility", "Upgraded", "", "", text);
        }

        // Kerbal Construction Time (optional): it queues a researched tech and unlocks it later without firing the stock
        // OnTechnologyResearched. It publishes OnKctTechCompleted by name; found at run time, so Scribe needs no reference to KCT.
        EventData<ProtoTechNode> kctTechDone;

        IEnumerator HookKct()
        {
            bool present = false;
            foreach (AssemblyLoader.LoadedAssembly a in AssemblyLoader.loadedAssemblies)
                if (a.name != null && a.name.StartsWith("KerbalConstructionTime", StringComparison.OrdinalIgnoreCase)) { present = true; break; }
            if (!present) yield break;
            for (int i = 0; i < 20 && kctTechDone == null; i++)
            {
                kctTechDone = GameEvents.FindEvent<EventData<ProtoTechNode>>("OnKctTechCompleted");
                if (kctTechDone == null) yield return new WaitForSecondsRealtime(1f);
            }
            if (kctTechDone != null) kctTechDone.Add(OnKctTechCompleted);
            else Debug.Log("[Scribe] Kerbal Construction Time is loaded but OnKctTechCompleted was not found; KCT tech completions will not be recorded");
        }

        void OnKctTechCompleted(ProtoTechNode n)
        {
            if (n != null) Event("tech", "TechCompleted", "", "", n.techID);
        }

        void OnContractAccepted(Contract c) { Event("contract", "Accepted", "", "", c.Title); }
        void OnContractCompleted(Contract c) { Event("contract", "Completed", "", "", c.Title); }
        void OnContractFailed(Contract c) { Event("contract", "Failed", "", "", c.Title); }
        void OnContractCancelled(Contract c) { Event("contract", "Cancelled", "", "", c.Title); }
    }
}
