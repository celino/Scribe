using System;
using System.Collections.Generic;
using Scribe;

namespace Scribe.Tests
{
    static class Regression
    {
        static int failures, checks;

        static void Check(bool ok, string what)
        {
            checks++;
            if (!ok) { failures++; Console.WriteLine("FAIL: " + what); }
        }

        static bool Near(double a, double b) { return Math.Abs(a - b) < 1e-9; }

        static int Main()
        {
            // 1. delta comes from the balance difference, not from the event value
            var l = new Ledger();
            l.SetBalance("funds", 716471.2);
            var e = l.RecordBalance("funds", 721471.2, 100, "ContractReward", "", "");
            Check(e != null && Near(e.Delta, 5000), "funds +5000 (29/10 table)");
            Check(e != null && Near(e.Balance, 721471.2), "balance kept");
            e = l.RecordBalance("funds", 719471.2, 101, "RnDPartPurchase", "", "");
            Check(e != null && Near(e.Delta, -2000), "funds -2000 is negative");
            e = l.RecordBalance("funds", 719171.2, 102, "CrewRecruited", "", "");
            Check(e != null && Near(e.Delta, -300), "funds -300");
            e = l.RecordBalance("funds", 719921.2, 103, "VesselRecovery", "", "");
            Check(e != null && Near(e.Delta, 750), "funds +750");

            // 2. reputation down is negative; science down too
            l.SetBalance("reputation", 399);
            e = l.RecordBalance("reputation", 405.38, 104, "ContractPenalty", "", "");
            Check(e != null && Near(e.Delta, 6.38), "rep delta by difference");
            l.SetBalance("science", 168.1);
            e = l.RecordBalance("science", 160.1, 105, "RnDTechResearch", "", "");
            Check(e != null && Near(e.Delta, -8), "science -8 negative");

            // 3. initial balance: first event without a known balance is only remembered
            var m = new Ledger();
            Check(m.RecordBalance("funds", 1000, 1, "x", "", "") == null, "no previous balance: no entry");
            Check(m.HasBalance("funds"), "balance remembered");
            e = m.RecordBalance("funds", 1500, 2, "x", "", "");
            Check(e != null && Near(e.Delta, 500), "second event measured against the first");
            // equal balance (repeated event): no entry
            Check(m.RecordBalance("funds", 1500, 3, "x", "", "") == null, "same balance, no entry");
            // kinds are independent
            m.SetBalance("science", 10);
            e = m.RecordBalance("science", 12, 4, "x", "", "");
            Check(e != null && Near(e.Delta, 2) && m.Entries.Count == 2, "science independent of funds");

            // 4. prune: cap, summary per day and type, totals preserved
            var p = new Ledger { Cap = 20 };
            p.SetBalance("funds", 0);
            double bal = 0, sum = 0;
            for (int i = 1; i <= 100; i++)
            {
                bal += i; sum += i;
                p.RecordBalance("funds", bal, i * 2000.0, "r", "", "");
            }
            Check(p.Entries.Count <= 20, "cap respected: " + p.Entries.Count);
            double total = 0; int counted = 0;
            foreach (var x in p.Entries) { total += x.Delta; counted += x.Count; }
            Check(Near(total, sum), "sum of deltas preserved by summaries");
            Check(counted == 100, "count preserved: " + counted);
            Check(p.Entries[0].Reason == "summary", "oldest is a summary");
            Check(Near(p.Entries[p.Entries.Count - 1].Balance, bal), "newest entry intact");
            // days far apart cannot all fit: still obeys the cap
            var q = new Ledger { Cap = 5 };
            for (int i = 0; i < 50; i++) q.RecordEvent("launch", "x", i * 21600.0, "n" + i, "Kerbin", "");
            Check(q.Entries.Count <= 5, "cap with one summary per day: " + q.Entries.Count);
            int qc = 0; foreach (var x in q.Entries) qc += x.Count;
            Check(qc == 50, "no entry lost in count: " + qc);

            // 5. round trip
            var r = new Ledger();
            r.SetBalance("funds", 100.5);
            r.RecordBalance("funds", 90.25, 12345.678, "VesselRecovery", "Aeris 3A", "Kerbin");
            r.RecordEvent("destroyed", "crash", 20000, "Jeb's \"Ship\"", "Mun", "line1\nline2 \\ end");
            var back = Ledger.FromText(Ledger.ToText(r.Entries));
            Check(back.Count == 2, "round trip count");
            Check(Near(back[0].Delta, -10.25) && Near(back[0].Ut, 12345.678) && Near(back[0].Balance, 90.25), "numbers survive");
            Check(back[0].Vessel == "Aeris 3A" && back[0].Reason == "VesselRecovery", "strings survive");
            Check(back[1].Vessel == "Jeb's \"Ship\"" && back[1].Text == "line1\nline2 \\ end", "escapes survive");
            // pairs round trip with a summary
            var s = new Entry { Type = "funds", Reason = "summary", Count = 7, Delta = -3.5, Balance = 9 };
            var s2 = Ledger.FromPairs(Ledger.ToPairs(s));
            Check(s2.Count == 7 && Near(s2.Delta, -3.5), "summary count survives");

            Console.WriteLine(checks + " checks, " + failures + " failures");
            return failures == 0 ? 0 : 1;
        }
    }
}
