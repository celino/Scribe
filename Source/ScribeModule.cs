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

        // One table for every game event: Hook adds them all, Unhook removes the same ones.
        List<KeyValuePair<Action, Action>> Subscriptions()
        {
            var t = new List<KeyValuePair<Action, Action>>();
            t.Add(Sub(() => GameEvents.OnFundsChanged.Add(OnFunds), () => GameEvents.OnFundsChanged.Remove(OnFunds)));
            t.Add(Sub(() => GameEvents.OnScienceChanged.Add(OnScience), () => GameEvents.OnScienceChanged.Remove(OnScience)));
            t.Add(Sub(() => GameEvents.OnReputationChanged.Add(OnReputation), () => GameEvents.OnReputationChanged.Remove(OnReputation)));
            t.Add(Sub(() => GameEvents.onLaunch.Add(OnLaunch), () => GameEvents.onLaunch.Remove(OnLaunch)));
            t.Add(Sub(() => GameEvents.onVesselRecovered.Add(OnRecovered), () => GameEvents.onVesselRecovered.Remove(OnRecovered)));
            t.Add(Sub(() => GameEvents.onVesselWillDestroy.Add(OnVesselDestroy), () => GameEvents.onVesselWillDestroy.Remove(OnVesselDestroy)));
            t.Add(Sub(() => GameEvents.onVesselDestroy.Add(OnVesselDestroy), () => GameEvents.onVesselDestroy.Remove(OnVesselDestroy)));
            t.Add(Sub(() => GameEvents.onKerbalStatusChange.Add(OnKerbalStatus), () => GameEvents.onKerbalStatusChange.Remove(OnKerbalStatus)));
            t.Add(Sub(() => GameEvents.onKerbalAdded.Add(OnKerbalAdded), () => GameEvents.onKerbalAdded.Remove(OnKerbalAdded)));
            t.Add(Sub(() => GameEvents.onPartCouple.Add(OnCouple), () => GameEvents.onPartCouple.Remove(OnCouple)));
            t.Add(Sub(() => GameEvents.OnTechnologyResearched.Add(OnTech), () => GameEvents.OnTechnologyResearched.Remove(OnTech)));
            t.Add(Sub(() => GameEvents.OnKSCFacilityUpgraded.Add(OnFacilityUpgraded), () => GameEvents.OnKSCFacilityUpgraded.Remove(OnFacilityUpgraded)));
            t.Add(Sub(() => GameEvents.Contract.onAccepted.Add(OnContractAccepted), () => GameEvents.Contract.onAccepted.Remove(OnContractAccepted)));
            t.Add(Sub(() => GameEvents.Contract.onCompleted.Add(OnContractCompleted), () => GameEvents.Contract.onCompleted.Remove(OnContractCompleted)));
            t.Add(Sub(() => GameEvents.Contract.onFailed.Add(OnContractFailed), () => GameEvents.Contract.onFailed.Remove(OnContractFailed)));
            t.Add(Sub(() => GameEvents.Contract.onCancelled.Add(OnContractCancelled), () => GameEvents.Contract.onCancelled.Remove(OnContractCancelled)));
            return t;
        }

        static KeyValuePair<Action, Action> Sub(Action add, Action remove)
        {
            return new KeyValuePair<Action, Action>(add, remove);
        }

        List<KeyValuePair<Action, Action>> subs;

        void Hook()
        {
            if (hooked) return;
            hooked = true;
            if (subs == null) subs = Subscriptions();
            foreach (var s in subs) s.Key();
            StartCoroutine(HookKct());
        }

        void Unhook()
        {
            if (!hooked) return;
            hooked = false;
            foreach (var s in subs) s.Value();
            if (kctTechDone != null) { kctTechDone.Remove(OnKctTechCompleted); kctTechDone = null; }
        }

        // ---- save / load ----

        public override void OnLoad(ConfigNode node)
        {
            if (node == null) return;
            Ledger = LedgerStore.Load(ToSaveNode(node));
            if (Ledger.Schema > ScribeInfo.Schema)
                Debug.LogWarning("[Scribe] This save was written by a newer Scribe (format " + Ledger.Schema + "; this one knows " + ScribeInfo.Schema
                    + "). Fields it does not know are kept as they are.");
            StartCoroutine(SeedBalances());
        }

        public override void OnSave(ConfigNode node)
        {
            if (node == null) return;
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
            if (Funding.Instance != null) Ledger.SeedBalance(Funds, Funding.Instance.Funds);
            if (ResearchAndDevelopment.Instance != null) Ledger.SeedBalance(Science, ResearchAndDevelopment.Instance.Science);
            if (Reputation.Instance != null) Ledger.SeedBalance(Rep, Reputation.Instance.reputation);
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
            try
            {
                string r = reason.ToString();
                bool ofVessel = ScribeRules.RelatesToVessel(r);   // a contract reward in flight is not the active vessel's
                Ledger.RecordBalance(kind, value, Now(), r, ofVessel ? ActiveVessel() : "", ofVessel ? ActiveBody() : "");
            }
            catch (Exception ex) { Debug.LogError("[Scribe] " + kind + ": " + ex); }
        }

        void Event(string type, string reason, string vessel, string body, string text)
        {
            try { Ledger.RecordEvent(type, reason, Now(), vessel, body, text); }
            catch (Exception ex) { Debug.LogError("[Scribe] " + type + ": " + ex); }
        }

        void ContractEvent(string reason, Contract c)
        {
            try
            {
                if (c == null) { Ledger.RecordEvent("contract", reason, Now(), "", "", ""); return; }
                var e = new Entry { Ut = Now(), Type = "contract", Reason = reason, Text = ScribeRules.TextOrEmpty(c.Title),
                                    Contract = c.ContractGuid.ToString(), Agent = c.Agent != null ? ScribeRules.TextOrEmpty(c.Agent.Name) : "" };
                Ledger.Add(e);
            }
            catch (Exception ex) { Debug.LogError("[Scribe] contract: " + ex); }
        }

        void KerbalEvent(string reason, ProtoCrewMember k, string vessel, string body)
        {
            try { Ledger.Add(new Entry { Ut = Now(), Type = "kerbal", Reason = reason, Vessel = vessel, Body = body, Text = k.name, Role = ScribeRules.TextOrEmpty(k.trait) }); }
            catch (Exception ex) { Debug.LogError("[Scribe] kerbal: " + ex); }
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
            var names = new List<string>();
            if (FlightGlobals.Bodies != null)
                foreach (CelestialBody b in FlightGlobals.Bodies) names.Add(b != null ? b.bodyName : null);
            return ScribeRules.BodyAt(names, index);
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
            KerbalEvent(reason, k, ActiveVessel(), ActiveBody());
        }

        void OnKerbalAdded(ProtoCrewMember k)
        {
            if (k != null) KerbalEvent("Added", k, "", "");
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
                if (ScribeRules.IsKctAssembly(a.name)) { present = true; break; }
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

        void OnContractAccepted(Contract c) { ContractEvent("Accepted", c); }
        void OnContractCompleted(Contract c) { ContractEvent("Completed", c); }
        void OnContractFailed(Contract c) { ContractEvent("Failed", c); }
        void OnContractCancelled(Contract c) { ContractEvent("Cancelled", c); }
    }
}
