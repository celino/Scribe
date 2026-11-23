// Scribe: the decisions the KSP module takes, as plain code with no Unity or KSP types (testable without the game).
// The module reads the game, turns what it sees into strings and numbers, and asks these classes what to do.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Scribe
{
    /// <summary>
    /// Decides which vessel destructions are losses. Debris, flags and merged vessels are not; one entry per vessel id.
    /// Time is passed in (seconds, any steady clock) so the rules can be tested without a running game.
    /// </summary>
    public class DestroyFilter
    {
        /// <summary>Seconds after a docking during which both vessels are ignored.</summary>
        public const float CoupleWindow = 3f;

        readonly Dictionary<Guid, float> coupled = new Dictionary<Guid, float>();
        readonly HashSet<Guid> seen = new HashSet<Guid>();

        /// <summary>Docking merges two vessels and the game destroys one of them: not a loss.</summary>
        public void NoteCoupled(Guid vesselId, float now)
        {
            coupled[vesselId] = now;
        }

        public bool JustCoupled(Guid vesselId, float now)
        {
            float t;
            return coupled.TryGetValue(vesselId, out t) && now - t < CoupleWindow;
        }

        /// <summary>
        /// True the first time a real loss is seen. <paramref name="vesselType"/> is the name of KSP's VesselType.
        /// Vessel.Die() fires onVesselWillDestroy before it sets the state to DEAD and onVesselDestroy comes later
        /// (often at scene unload); both are hooked, and the id keeps one entry per vessel.
        /// </summary>
        public bool ShouldRecord(Guid vesselId, string vesselType, int partCount, float now)
        {
            if (JustCoupled(vesselId, now)) return false;
            if (!ScribeRules.IsRecordableVessel(vesselType)) return false;
            if (partCount <= 0) return false;   // nothing left: merged by docking
            return seen.Add(vesselId);
        }
    }

    /// <summary>The game can fire the facility upgrade event once per facility object: the same text within a second is one upgrade.</summary>
    public class FacilityDebounce
    {
        public const float Window = 1f;
        string last;
        float lastAt = -10f;

        public bool Accept(string text, float now)
        {
            if (text == last && now - lastAt < Window) return false;
            last = text;
            lastAt = now;
            return true;
        }
    }

    public static class ScribeRules
    {
        /// <summary>Debris, flags and space objects (asteroids, comets) are not vessels worth a ledger line. <paramref name="vesselType"/> is KSP's VesselType name.</summary>
        public static bool IsRecordableVessel(string vesselType)
        {
            return vesselType != "Debris" && vesselType != "Flag" && vesselType != "SpaceObject";
        }

        /// <summary>Which roster statuses are worth an entry. <paramref name="to"/> is the name of KSP's RosterStatus; null when not recorded.</summary>
        public static string KerbalReason(string to)
        {
            if (to == "Dead") return "Dead";
            if (to == "Missing") return "Missing";
            return null;
        }

        /// <summary>The text of a facility upgrade entry. <paramref name="levelIndex"/> counts from 0, the entry says "level 2" for index 1.</summary>
        public static string FacilityText(string facilityId, int levelIndex)
        {
            return facilityId + " level " + (levelIndex + 1).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>ConfigNode values are one line and "//" starts a comment when the file is read back.</summary>
        public static string Clean(string s)
        {
            return s.Replace("\r", " ").Replace("\n", " ").Replace("//", "/ /");
        }
    }

    /// <summary>A tree of name/value pairs: the shape of a KSP ConfigNode without the game, so the save format can be tested.</summary>
    public class SaveNode
    {
        public readonly List<KeyValuePair<string, string>> Values = new List<KeyValuePair<string, string>>();
        public readonly List<KeyValuePair<string, SaveNode>> Nodes = new List<KeyValuePair<string, SaveNode>>();

        public void AddValue(string name, string value)
        {
            Values.Add(new KeyValuePair<string, string>(name, value));
        }

        public SaveNode AddNode(string name)
        {
            var n = new SaveNode();
            Nodes.Add(new KeyValuePair<string, SaveNode>(name, n));
            return n;
        }

        public string GetValue(string name)
        {
            foreach (var v in Values) if (v.Key == name) return v.Value;
            return null;
        }

        public SaveNode GetNode(string name)
        {
            foreach (var n in Nodes) if (n.Key == name) return n.Value;
            return null;
        }
    }

    /// <summary>What the SCRIBE node of persistent.sfs holds, and how it is read and written.</summary>
    public static class LedgerStore
    {
        public const string BalancesNode = "BALANCES";

        public static Ledger Load(SaveNode node)
        {
            var ledger = new Ledger();
            int cap;
            if (int.TryParse(node.GetValue("cap"), NumberStyles.Integer, CultureInfo.InvariantCulture, out cap) && cap > 0)
                ledger.Cap = cap;
            foreach (var n in node.Nodes)
                if (n.Key == ScribeInfo.EntryName) ledger.Entries.Add(Ledger.FromPairs(n.Value.Values));
            // last known balances saved with the ledger: they belong to this very save, so a revert/quickload is consistent
            SaveNode b = node.GetNode(BalancesNode);
            if (b != null)
                foreach (var v in b.Values)
                {
                    double d;
                    if (double.TryParse(v.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                        ledger.SetBalance(v.Key, d);
                }
            return ledger;
        }

        public static void Save(Ledger ledger, SaveNode node)
        {
            node.AddValue("cap", ledger.Cap.ToString(CultureInfo.InvariantCulture));
            SaveNode b = node.AddNode(BalancesNode);
            foreach (var kv in ledger.Balances) b.AddValue(kv.Key, Ledger.Num(kv.Value));
            foreach (Entry e in ledger.Entries)
            {
                SaveNode n = node.AddNode(ScribeInfo.EntryName);
                foreach (var p in Ledger.ToPairs(e)) n.AddValue(p.Key, ScribeRules.Clean(p.Value));
            }
        }
    }
}
