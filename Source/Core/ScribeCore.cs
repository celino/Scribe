// Scribe: pure logic, no UnityEngine and no KSP types (testable without the game).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Scribe
{
    public static class ScribeInfo
    {
        public const string NodeName = "SCRIBE";
        public const string EntryName = "ENTRY";
        public const int DefaultCap = 5000;
        public const double KerbinDay = 21600.0;
    }

    /// <summary>One line of the ledger.</summary>
    public class Entry
    {
        public double Ut;
        public string Type = "";     // funds | science | reputation | launch | recovered | destroyed | kerbal | contract | tech | summary
        public string Reason = "";   // TransactionReasons name, event name, or "summary"
        public double Delta;         // real change (new balance minus previous balance)
        public double Balance;       // balance after the change (0 when not a balance entry)
        public string Vessel = "";
        public string Body = "";
        public string Text = "";
        public int Count = 1;        // >1 only in summaries: how many entries were folded

        public bool IsBalance
        {
            get { return Type == "funds" || Type == "science" || Type == "reputation"; }
        }
    }

    public class Ledger
    {
        public readonly List<Entry> Entries = new List<Entry>();
        public int Cap = ScribeInfo.DefaultCap;
        readonly Dictionary<string, double> last = new Dictionary<string, double>();

        /// <summary>Sets the balance the next change is measured against (on load, or when a scene starts).</summary>
        public void SetBalance(string kind, double balance)
        {
            last[kind] = balance;
        }

        public IEnumerable<KeyValuePair<string, double>> Balances
        {
            get { return last; }
        }

        public bool HasBalance(string kind)
        {
            return last.ContainsKey(kind);
        }

        /// <summary>
        /// KSP's OnFundsChanged / OnScienceChanged / OnReputationChanged pass the NEW balance.
        /// The change is that balance minus the last one we know. Returns null when nothing changed
        /// or when there is no previous balance (then the balance is only remembered).
        /// </summary>
        public Entry RecordBalance(string kind, double newBalance, double ut, string reason, string vessel, string body)
        {
            double previous;
            bool known = last.TryGetValue(kind, out previous);
            last[kind] = newBalance;
            if (!known) return null;
            double delta = newBalance - previous;
            if (delta == 0.0) return null;
            var e = new Entry { Ut = ut, Type = kind, Reason = reason ?? "", Delta = delta, Balance = newBalance,
                                Vessel = vessel ?? "", Body = body ?? "" };
            Add(e);
            return e;
        }

        public Entry RecordEvent(string type, string reason, double ut, string vessel, string body, string text)
        {
            var e = new Entry { Ut = ut, Type = type, Reason = reason ?? "", Vessel = vessel ?? "", Body = body ?? "", Text = text ?? "" };
            Add(e);
            return e;
        }

        public void Add(Entry e)
        {
            Entries.Add(e);
            if (Entries.Count > Cap) Prune();
        }

        /// <summary>
        /// Folds the oldest entries into one summary per (Kerbin day, type) until the ledger fits the cap.
        /// Folds about 10% extra so it does not run on every add. Summaries keep the summed delta and the last balance.
        /// </summary>
        public void Prune()
        {
            if (Cap < 1) Cap = 1;
            if (Entries.Count <= Cap) return;
            int fold = Entries.Count - Cap + Math.Max(1, Cap / 10);
            double bucket = ScribeInfo.KerbinDay; // widens when one summary per day does not fit
            int level = 0;
            while (true)
            {
                int n = Math.Min(fold, Entries.Count);
                var head = Entries.GetRange(0, n);
                var tail = Entries.GetRange(n, Entries.Count - n);
                var merged = new List<Entry>();
                var index = new Dictionary<string, Entry>();
                foreach (var e in head)
                {
                    long day = level >= 40 ? 0 : (long)Math.Floor(e.Ut / bucket);
                    string key = day.ToString(CultureInfo.InvariantCulture) + "|" + (level >= 41 ? "" : e.Type);
                    Entry s;
                    if (!index.TryGetValue(key, out s))
                    {
                        s = new Entry { Ut = day * bucket, Type = level >= 41 ? "summary" : e.Type, Reason = "summary", Count = 0 };
                        index[key] = s;
                        merged.Add(s);
                    }
                    s.Count += e.Count;
                    s.Delta += e.Delta;
                    if (e.IsBalance) s.Balance = e.Balance;
                }
                foreach (var s in merged) s.Text = "folded " + s.Count.ToString(CultureInfo.InvariantCulture) + " entries";
                Entries.Clear();
                Entries.AddRange(merged);
                Entries.AddRange(tail);
                if (Entries.Count <= Cap) return;
                // summaries alone did not fit: fold more, with wider buckets; at the end one summary per type, then one
                fold = Entries.Count;
                level++;
                bucket *= 2;
                if (level > 20 && level < 40) level = 40;
            }
        }

        // ---- serialization: flat key/value pairs, so the module can put them in a ConfigNode ----

        public static string Num(double v)
        {
            return v.ToString("R", CultureInfo.InvariantCulture);
        }

        public static List<KeyValuePair<string, string>> ToPairs(Entry e)
        {
            var l = new List<KeyValuePair<string, string>>();
            l.Add(new KeyValuePair<string, string>("ut", Num(e.Ut)));
            l.Add(new KeyValuePair<string, string>("type", e.Type));
            l.Add(new KeyValuePair<string, string>("reason", e.Reason));
            l.Add(new KeyValuePair<string, string>("delta", Num(e.Delta)));
            l.Add(new KeyValuePair<string, string>("balance", Num(e.Balance)));
            if (e.Vessel.Length > 0) l.Add(new KeyValuePair<string, string>("vessel", e.Vessel));
            if (e.Body.Length > 0) l.Add(new KeyValuePair<string, string>("body", e.Body));
            if (e.Text.Length > 0) l.Add(new KeyValuePair<string, string>("text", e.Text));
            if (e.Count != 1) l.Add(new KeyValuePair<string, string>("count", e.Count.ToString(CultureInfo.InvariantCulture)));
            return l;
        }

        public static Entry FromPairs(IEnumerable<KeyValuePair<string, string>> pairs)
        {
            var e = new Entry();
            foreach (var p in pairs)
            {
                switch (p.Key)
                {
                    case "ut": e.Ut = ParseNum(p.Value); break;
                    case "type": e.Type = p.Value; break;
                    case "reason": e.Reason = p.Value; break;
                    case "delta": e.Delta = ParseNum(p.Value); break;
                    case "balance": e.Balance = ParseNum(p.Value); break;
                    case "vessel": e.Vessel = p.Value; break;
                    case "body": e.Body = p.Value; break;
                    case "text": e.Text = p.Value; break;
                    case "count":
                        int c;
                        e.Count = int.TryParse(p.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out c) ? c : 1;
                        break;
                }
            }
            return e;
        }

        static double ParseNum(string s)
        {
            double v;
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0.0;
        }

        /// <summary>Plain-text form (one block per entry), used by the tests to check the round trip.</summary>
        public static string ToText(IEnumerable<Entry> entries)
        {
            var sb = new StringBuilder();
            foreach (var e in entries)
            {
                sb.Append("ENTRY\n");
                foreach (var p in ToPairs(e)) sb.Append(p.Key).Append(" = ").Append(Escape(p.Value)).Append('\n');
                sb.Append("END\n");
            }
            return sb.ToString();
        }

        public static List<Entry> FromText(string text)
        {
            var result = new List<Entry>();
            List<KeyValuePair<string, string>> cur = null;
            foreach (var raw in text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line == "ENTRY") cur = new List<KeyValuePair<string, string>>();
                else if (line == "END") { if (cur != null) result.Add(FromPairs(cur)); cur = null; }
                else if (cur != null)
                {
                    int i = line.IndexOf(" = ", StringComparison.Ordinal);
                    if (i > 0) cur.Add(new KeyValuePair<string, string>(line.Substring(0, i), Unescape(line.Substring(i + 3))));
                }
            }
            return result;
        }

        static string Escape(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        static string Unescape(string s)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    i++;
                    sb.Append(s[i] == 'n' ? '\n' : s[i] == 'r' ? '\r' : s[i]);
                }
                else sb.Append(s[i]);
            }
            return sb.ToString();
        }
    }
}
