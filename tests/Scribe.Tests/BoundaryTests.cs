using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scribe.Tests
{
    // Closes the gaps Stryker found (docs/MUTATION.md): exact boundaries, the shape of the folded ledger, every field after a
    // round trip, and properties of the fold that must hold for any sequence of entries.
    public class BoundaryTests
    {
        static Ledger Funds(double balance)
        {
            var l = new Ledger();
            l.SetBalance("funds", balance);
            return l;
        }

        // ---- F18 grouping: exact window and lookback ----

        [Fact, Trait("Feature", "F18")]
        public void F18_a_change_exactly_one_window_later_still_joins()
        {
            var l = Funds(1000);
            l.RecordBalance("funds", 900, 10.0, "Structures", "", "");
            var joined = l.RecordBalance("funds", 800, 10.0 + Ledger.GroupWindow, "Structures", "", "");
            Assert.Single(l.Entries);
            Assert.Same(l.Entries[0], joined);
            Assert.Equal(2, l.Entries[0].Count);
        }

        [Fact, Trait("Feature", "F18")]
        public void F18_a_change_just_past_the_window_is_a_new_entry()
        {
            var l = Funds(1000);
            l.RecordBalance("funds", 900, 10.0, "Structures", "", "");
            l.RecordBalance("funds", 800, 10.0 + Ledger.GroupWindow + 0.001, "Structures", "", "");
            Assert.Equal(2, l.Entries.Count);
        }

        [Theory, Trait("Feature", "F18")]
        [InlineData(Ledger.GroupLookback - 1, true)]   // the match is the last one the search looks at
        [InlineData(Ledger.GroupLookback, false)]      // one further back: not looked at
        public void F18_grouping_looks_back_exactly_the_lookback(int entriesBetween, bool joins)
        {
            var l = Funds(1000);
            l.RecordBalance("funds", 900, 5.0, "Structures", "", "");
            for (int i = 0; i < entriesBetween; i++) l.RecordEvent("launch", "Launch", 5.0, "V" + i, "", "");
            l.RecordBalance("funds", 800, 5.0, "Structures", "", "");
            Assert.Equal(joins ? 1 + entriesBetween : 2 + entriesBetween, l.Entries.Count);
        }

        [Fact, Trait("Feature", "F18")]
        public void F18_different_sign_vessel_body_or_reason_do_not_join()
        {
            var l = Funds(1000);
            l.RecordBalance("funds", 900, 1.0, "R", "V", "Kerbin");
            l.RecordBalance("funds", 950, 1.0, "R", "V", "Kerbin");    // credit
            l.RecordBalance("funds", 850, 1.0, "R", "W", "Kerbin");    // other vessel
            l.RecordBalance("funds", 750, 1.0, "R", "V", "Mun");       // other body
            l.RecordBalance("funds", 650, 1.0, "Q", "V", "Kerbin");    // other reason
            Assert.Equal(5, l.Entries.Count);
        }

        // ---- F03 cap: exactly at the cap nothing folds, one more does ----

        [Fact, Trait("Feature", "F03")]
        public void F03_at_the_cap_nothing_is_folded_one_more_folds()
        {
            var l = new Ledger { Cap = 10 };
            for (int i = 0; i < 10; i++) l.RecordEvent("launch", "Launch", 100 + i, "V", "", "");
            Assert.Equal(10, l.Entries.Count);
            Assert.DoesNotContain(l.Entries, e => e.Reason == "summary");
            l.RecordEvent("launch", "Launch", 200, "V", "", "");
            Assert.True(l.Entries.Count <= 10);
            Assert.Contains(l.Entries, e => e.Reason == "summary");
        }

        [Fact, Trait("Feature", "F03")]
        public void F03_a_cap_below_one_is_raised_to_one()
        {
            var l = new Ledger { Cap = 0 };
            l.RecordEvent("launch", "Launch", 1, "V", "", "");
            l.RecordEvent("launch", "Launch", 2, "V", "", "");
            Assert.Equal(1, l.Cap);
            Assert.Single(l.Entries);
            Assert.Equal(2, l.Entries[0].Count);
        }

        // ---- F03 the shape of the folded ledger ----

        [Fact, Trait("Feature", "F03")]
        public void F03_the_oldest_two_entries_of_a_day_and_type_become_one_summary()
        {
            // 11 entries over cap 10: folds 11 - 10 + max(1, 10/10) = 2 of the oldest
            var l = new Ledger { Cap = 10 };
            for (int i = 0; i < 11; i++) l.RecordEvent("launch", "Launch", 100 + i, "V", "", "");
            var s = l.Entries[0];
            Assert.Equal(10, l.Entries.Count);
            Assert.Equal("summary", s.Reason);
            Assert.Equal("launch", s.Type);
            Assert.Equal(0.0, s.Ut, 6);                       // the start of Kerbin day 0
            Assert.Equal(2, s.Count);
            Assert.Equal("folded 2 entries", s.Text);
            Assert.Equal(102.0, l.Entries[1].Ut, 6);          // the rest is untouched, in order
        }

        [Fact, Trait("Feature", "F03")]
        public void F03_summaries_keep_the_totals_and_the_last_balance()
        {
            var l = new Ledger { Cap = 10 };
            l.SetBalance("funds", 1000);
            l.RecordBalance("funds", 900, 10, "A", "", "");             // day 0, funds
            l.RecordEvent("launch", "Launch", 20, "V", "", "");         // day 0, launch
            l.RecordBalance("funds", 700, ScribeInfo.KerbinDay + 5, "B", "", "");   // day 1, funds
            l.RecordBalance("funds", 600, ScribeInfo.KerbinDay + 50, "C", "", "");  // day 1, funds, new instant
            for (int i = 0; i < 8; i++) l.RecordEvent("launch", "Launch", 5 * ScribeInfo.KerbinDay + i * 10, "V", "", "");
            Assert.True(l.Entries.Count <= 10);
            var sums = l.Entries.Where(e => e.Reason == "summary").ToList();
            Assert.True(sums.Count >= 2);
            double totalFunds = sums.Where(e => e.Type == "funds").Sum(e => e.Delta) + l.Entries.Where(e => e.Reason != "summary" && e.Type == "funds").Sum(e => e.Delta);
            Assert.Equal(-400.0, totalFunds, 6);
            // the funds summary starts on day 0 and carries the last funds balance it folded (the buckets may have widened to hold the rest)
            var f = sums.First(e => e.Type == "funds");
            Assert.Equal(0.0, f.Ut, 6);
            Assert.Equal(600.0, l.Entries.Last(e => e.Type == "funds").Balance, 6);
        }

        [Fact, Trait("Feature", "F04")]
        public void F04_when_one_summary_per_day_does_not_fit_the_buckets_widen_and_the_cap_still_holds()
        {
            var l = new Ledger { Cap = 5 };
            for (int i = 0; i < 40; i++) l.RecordEvent(i % 2 == 0 ? "launch" : "tech", "x", i * 30 * ScribeInfo.KerbinDay, "", "", "");
            Assert.True(l.Entries.Count <= 5);
            Assert.Equal(40, l.Entries.Sum(e => e.Count));
        }

        // ---- F05 every field survives a round trip; empty fields are not written ----

        [Fact, Trait("Feature", "F05")]
        public void F05_every_field_survives_a_round_trip()
        {
            var e = new Entry { Ut = 12345.678901234, Type = "funds", Reason = "Structures", Delta = -40000.5, Balance = 59999.5,
                                Vessel = "Kerbal X", Body = "Kerbin", Text = "level 2", Count = 7 };
            var back = Ledger.FromText(Ledger.ToText(new[] { e })).Single();
            Assert.Equal(e.Ut, back.Ut);
            Assert.Equal(e.Type, back.Type);
            Assert.Equal(e.Reason, back.Reason);
            Assert.Equal(e.Delta, back.Delta);
            Assert.Equal(e.Balance, back.Balance);
            Assert.Equal(e.Vessel, back.Vessel);
            Assert.Equal(e.Body, back.Body);
            Assert.Equal(e.Text, back.Text);
            Assert.Equal(e.Count, back.Count);
        }

        [Fact, Trait("Feature", "F05")]
        public void F05_empty_fields_and_a_count_of_one_are_not_written()
        {
            var keys = Ledger.ToPairs(new Entry { Type = "launch", Reason = "Launch" }).Select(p => p.Key).ToList();
            Assert.Equal(string.Join(",", "ut", "type", "reason", "delta", "balance"), string.Join(",", keys));
        }

        [Fact, Trait("Feature", "F05")]
        public void F05_a_carriage_return_and_a_backslash_survive()
        {
            var e = new Entry { Type = "launch", Reason = "Launch", Vessel = "a\rb\\c\\", Text = "x\\ny" };
            var back = Ledger.FromText(Ledger.ToText(new[] { e })).Single();
            Assert.Equal(e.Vessel, back.Vessel);
            Assert.Equal(e.Text, back.Text);
        }

        [Fact, Trait("Feature", "F05")]
        public void F05_a_lone_trailing_backslash_in_a_hand_edited_file_is_kept()
        {
            var back = Ledger.FromText("ENTRY\ntype = launch\nvessel = ab\\\nEND\n").Single();
            Assert.Equal("ab\\", back.Vessel);
        }

        [Theory, Trait("Feature", "F05")]
        [InlineData("funds", true)]
        [InlineData("science", true)]
        [InlineData("reputation", true)]
        [InlineData("launch", false)]
        [InlineData("summary", false)]
        [InlineData("", false)]
        public void F05_only_funds_science_and_reputation_are_balances(string type, bool expected)
        {
            Assert.Equal(expected, new Entry { Type = type }.IsBalance);
        }

        // ---- properties of the fold, over random sequences (fixed seeds, so a failure repeats) ----

        [Theory, Trait("Feature", "F03")]
        [InlineData(1, 20)]
        [InlineData(2, 50)]
        [InlineData(3, 200)]
        [InlineData(4, 7)]
        public void F03_for_any_sequence_the_cap_holds_and_nothing_is_lost(int seed, int cap)
        {
            var rnd = new Random(seed);
            var l = new Ledger { Cap = cap };
            string[] kinds = { "funds", "science", "reputation" };
            var balance = new Dictionary<string, double>();
            var expectedDelta = new Dictionary<string, double>();
            int recorded = 0, events = 0;
            double ut = 0;
            foreach (var k in kinds) { l.SetBalance(k, 0); balance[k] = 0; expectedDelta[k] = 0; }
            for (int i = 0; i < 3000; i++)
            {
                ut += rnd.Next(4) == 0 ? rnd.Next(1, 4) * ScribeInfo.KerbinDay : rnd.NextDouble() * 5;
                if (rnd.Next(3) == 0) { l.RecordEvent(rnd.Next(2) == 0 ? "launch" : "tech", "x", ut, "V" + rnd.Next(3), "", ""); events++; recorded++; }
                else
                {
                    string k = kinds[rnd.Next(kinds.Length)];
                    int step = rnd.Next(-500, 501);
                    if (step == 0) continue;
                    balance[k] += step; expectedDelta[k] += step;
                    l.RecordBalance(k, balance[k], ut, "R" + rnd.Next(4), "V" + rnd.Next(2), "Kerbin");
                    recorded++;
                }
                Assert.True(l.Entries.Count <= cap, "cap broken at step " + i);
            }
            // how many entries the game recorded is kept (grouped and folded entries carry their count)
            Assert.Equal(recorded, l.Entries.Sum(e => e.Count));
            foreach (var k in kinds)
                Assert.Equal(expectedDelta[k], l.Entries.Where(e => e.Type == k).Sum(e => e.Delta), 6);
        }

        [Fact, Trait("Feature", "F03")]
        public void F03_the_fold_ends_even_with_the_smallest_cap_and_one_type()
        {
            // the fold once looped forever; a cap of 1 with many days apart is the hardest case
            var l = new Ledger { Cap = 1 };
            for (int i = 0; i < 100; i++) l.RecordEvent("launch", "Launch", i * ScribeInfo.KerbinDay * 3, "V", "", "");
            Assert.Single(l.Entries);
            Assert.Equal(100, l.Entries[0].Count);
        }
    }
}
